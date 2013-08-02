﻿using System;
using System.Collections.Generic;
using System.Linq;

using System.Xml.Linq;

using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Models; 
using Umbraco.Core.Logging;

using System.IO;

using System.Text.RegularExpressions;
using System.Diagnostics;

using jumoo.usync.content.helpers;

namespace jumoo.usync.content
{
    /// <summary>
    ///  uSync Content importer - takes content from the disk and puts it into
    ///  the umbraco installation.
    /// </summary>
    public class ContentImporter
    {
        PackagingService _packager;
        IContentService _contentService;

        Dictionary<int, int> _idMap = new Dictionary<int, int>();

        int importCount = 0; // used just to say how many things we imported

        public ContentImporter()
        {
            _packager = ApplicationContext.Current.Services.PackagingService;
            _contentService = ApplicationContext.Current.Services.ContentService;

            // load the import table (from disk)
            ImportPairs.LoadFromDisk(); 
        }

        /// <summary>
        ///  import the disk content
        /// </summary>
        /// <param name="mapIds">do we attempt to fix the internal id mappings in the content</param>
        /// <returns></returns>
        public int ImportDiskContent(bool mapIds)
        {
            LogHelper.Info<ContentImporter>("Import Starting MapId = {0}", () => mapIds);
            Stopwatch sw = Stopwatch.StartNew(); 

            importCount = 0;

            string root = helpers.FileHelper.uSyncRoot;

            ImportDiskContent(root, -1, mapIds);

            // save the import pair table.
            // SaveImportPairTable();
            ImportPairs.SaveToDisk();

            sw.Stop();
            LogHelper.Info<ContentImporter>("Import Complete [{0} milliseconds]", () => sw.Elapsed.TotalMilliseconds); 

            return importCount; 
        }

        /// <summary>
        ///  walks the disk folder, imports any .content files it finds
        ///  in a folder, then recurses into sub folders to do the same
        /// </summary>
        /// <param name="path">path on disk to this folder</param>
        /// <param name="parentId">ID of parent content</param>
        /// <param name="mapIds">do we map internal ids inside the content nodes</param>
        public void ImportDiskContent(string path, int parentId, bool mapIds)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.content"))
                {
                    // LogHelper.Info(typeof(ContentImporter), String.Format("Found Content File {0}", file)); 

                    XElement element = XElement.Load(file);

                    if (element != null)
                    {
                        IContent item = ImportContentItem(element, parentId, mapIds);

                        if (item != null)
                        {
                            string folderPath = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));

                            if (Directory.Exists(folderPath))
                            {
                                ImportDiskContent(folderPath, item.Id, mapIds);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///  imports a bit of content based on the xml it gets passed.
        /// </summary>
        /// <param name="element">xml of content node</param>
        /// <param name="parentId">id of parent node</param>
        /// <param name="mapIds">do we map internal content ids</param>
        /// <returns>IContent node of newly updated / created content</returns>
        public IContent ImportContentItem(XElement element, int parentId, bool mapIds)
        {
            bool _new = false; // flag to track if we created new content.

            LogHelper.Info<ContentImporter>( "Importing Content Item {0} [Mapping={1}]", () => element.Attribute("nodeName").Value, () => mapIds ); 

            // get the guid from the xml
            Guid contentGuid = new Guid(element.Attribute("guid").Value);

            // gets the guid we will use for import 
            Guid _guid = helpers.ImportPairs.GetTargetGuid(contentGuid);

            // load all the additonal values from the xml 
            string name = element.Attribute("nodeName").Value;
            string nodeType = element.Attribute("nodeTypeAlias").Value;
            string templateAlias = element.Attribute("templateAlias").Value;

            int sortOrder = int.Parse(element.Attribute("sortOrder").Value);
            bool published = bool.Parse(element.Attribute("published").Value);

            DateTime updateDate = DateTime.Now; 

            if (element.Attribute("updated") != null)
            {
                updateDate = DateTime.Parse(element.Attribute("updated").Value);
            }


            // try to load the content. 
            // even if we haven't imported it before, we might be
            // reimporting to a source system so we should have a go
            IContent content = _contentService.GetById(_guid);
            if (content == null)
            {
                // this is new..
                content = _contentService.CreateContentWithIdentity(name, parentId, nodeType);
                LogHelper.Debug<ContentImporter>("Created New Content Node");
                _new = true; 
            }
            else
            {
                // log..
                if (content.Trashed == true)
                {
                    // it's in the bin, create a new version 
                    content = _contentService.CreateContentWithIdentity(name, parentId, nodeType);
                    LogHelper.Debug<ContentImporter>("Node was in bin, creating new node");
                    _new = true;
                }
                else
                {
                    //
                    // logic is if the file update is newer than what we have on disk, then we should
                    // run through the update. not convinced by this. but it does speed up the appstart
                    // significantly for large sites.
                    //
                    if (DateTime.Compare(updateDate, content.UpdateDate) <= 0)
                    {
                        LogHelper.Info<ContentImporter>("Content has not changed since last read from disk");
                        return content;
                    }
                    LogHelper.Debug<ContentImporter>("Updating existing node");
                }
            }
        

            if (content != null)
            {
                // now we have content
                ITemplate template = ApplicationContext.Current.Services.FileService.GetTemplate(templateAlias);

                if (template != null)
                    content.Template = template;

                content.SortOrder = sortOrder; 

                // load all the properties 
                var properties = from property in element.Elements()
                                 where property.Attribute("isDoc") == null
                                 select property;

                foreach (var property in properties)
                {
                    // LogHelper.Info(typeof(ContentImporter), String.Format("Property: {0}", property.Name)); 

                    string propertyTypeAlias = property.Name.LocalName;
                    if (content.HasProperty(propertyTypeAlias))
                    {
                        // right if we are trying to be clever and map ids 
                        // then mapIds will be set
                        if (mapIds)
                        {
                            // we can only really do this once all the content is imported.
                            // so it's a bit of a two pass thing.
                            content.SetValue(propertyTypeAlias, UpdateMatchingIds(GetInnerXML(property)));

                        }
                        else
                        {
                            // just map the values
                            content.SetValue(propertyTypeAlias, GetInnerXML(property));
                        }
                    }
                }

                if (content.Trashed)
                {
                    // TODO: something with trashed content ? 
                    // if the content is in the bin - then we've still found it - we should undelete it ?
                    //
                    // other option is to check this earlier and just create new content
                    // 
                }
                    

                // do we publish?
                if (published)
                {
                    _contentService.SaveAndPublish(content, 0, false);
                }
                else
                {
                    _contentService.Save(content, 0, false);
                    
                    // if it was already published we should unpublish here..
                    if (content.Published)
                    {
                        _contentService.UnPublish(content);
                    }
                }



                ++importCount; // for status updates...

                if (_new)
                {
                    // it's new add it to the import table
                    helpers.ImportPairs.SavePair(contentGuid, content.Key);
                    
                }

                // add the content to the id map (for second pass mapping)
                int _sourceId = int.Parse(element.Attribute("id").Value);
                if (!_idMap.ContainsKey(_sourceId))
                {
                    _idMap.Add(_sourceId, content.Id);
                }

                //
                // the reality is the GUID is no special thing here. 
                // as all new content gets a new guid, you could just 
                // as well use the ID. you can't force the guid on content
                // so their is no value, we end up mapping oldguid -> new
                // and old id -> new - when one would do, and as umbraco
                // uses id everywhere, you could just map that. 
                // 

                return content; 

            }

            return null; 

        }

        /// <summary>
        ///  takes the content, uses the IdMap to search and replace
        ///  and id's it finds in the code.
        /// </summary>
        /// <param name="content">piece of content to check</param>
        /// <returns>updated content with new id values</returns>
        private string UpdateMatchingIds(string content)
        {
            LogHelper.Debug<ContentImporter>("Original [{0}]", () => content);
            
            Dictionary<string, string> replacements = new Dictionary<string, string>();

            string guidRegEx = @"\b[A-F0-9]{8}(?:-[A-F0-9]{4}){3}-[A-F0-9]{12}\b";  

            // look for things that might be Guids...
            foreach(Match m in Regex.Matches(content, guidRegEx))
            {
                int id = GetIdFromGuid(Guid.Parse(m.Value));

                if ( !replacements.ContainsKey(m.Value))
                {
                    replacements.Add(m.Value, id.ToString() );
                }
            }

            // now loop through our replacements and add them

            foreach(KeyValuePair<string, string> pair in replacements)
            {
                LogHelper.Debug<ContentImporter>( "Updating Id's {0} > {1}", () => pair.Key, () => pair.Value); 
                content = content.Replace(pair.Key, pair.Value);
            }

            LogHelper.Debug(typeof(ContentImporter), String.Format("Updated [{0}]", content));
            return content; 
        }


        // some variables have inside xml, so we need to get to that
        // and not let the parser take it out
        private string GetInnerXML(XElement parent)
        {
            var reader = parent.CreateReader();
            reader.MoveToContent();
            string xml = reader.ReadInnerXml();

            // except umbraco then doesn't like content
            // starting cdata
            if (xml.StartsWith("<![CDATA["))
            {
                return parent.Value;
            }
            return xml; 
        }

        private int GetIdFromGuid(Guid guid)
        {
            Guid sourceGuid = helpers.ImportPairs.GetSourceGuid(guid);

            ContentService cs = new ContentService();
            IContent c = cs.GetById(sourceGuid);
            if (c != null)
                return c.Id;
            else
                return 1000;
        }

    }
}
