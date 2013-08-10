﻿
using System.IO ; 
using Umbraco.Core.IO ; 

using Umbraco.Core ;
using Umbraco.Core.Models;
using Umbraco.Core.Logging;

using System.Xml.Linq;
using System;
using System.Text; 

namespace jumoo.usync.content.helpers
{
    public class FileHelper
    {
        static string _mappedRoot = "";
        static string _mappedArchive = "";
        static string _mappedTemp = ""; // this must be in umbraco somewhere...
        static string _mappedMediaRoot = "";
        static string _mappedFilesRoot = "";

        static FileHelper()
        {
            _mappedRoot = IOHelper.MapPath(uSyncContentSettings.Folder);
            _mappedArchive = IOHelper.MapPath(uSyncContentSettings.ArchiveFolder);
            _mappedTemp = IOHelper.MapPath("~/App_data/Temp/");
            _mappedMediaRoot = IOHelper.MapPath(uSyncContentSettings.MediaFolder);
            _mappedFilesRoot = IOHelper.MapPath(uSyncContentSettings.Files); 

            if (!Directory.Exists(_mappedRoot))
                Directory.CreateDirectory(_mappedRoot);
        }

        public static string uSyncRoot
        {
            get { return _mappedRoot; }
        }

        public static string uSyncTemp
        {
            get { return _mappedTemp; }
        }

        public static string uSyncMediaRoot
        {
            get { return _mappedMediaRoot; }
        }



        public static bool SaveMediaFile(string path, IMedia media, XElement element)
        {
            LogHelper.Debug<FileHelper>("SaveMedia File {0} {1}", () => path, () => media.Name);

            string filename = string.Format("{0}.media", CleanFileName(media.Name));
            string fullpath = Path.Combine(string.Format("{0}{1}", _mappedMediaRoot, path), filename);

            return SaveContentBaseFile(fullpath, element); 

        }

        public static bool SaveContentFile(string path, IContent node, XElement element)
        {
            LogHelper.Debug<FileHelper>("SaveContentFile {0} {1}", () => path, () => node.Name);

            string filename = string.Format("{0}.content", CleanFileName(node.Name));
            string fullpath = Path.Combine(string.Format("{0}{1}", _mappedRoot, path), filename);

            return SaveContentBaseFile(fullpath, element) ; 
        }

        private static bool SaveContentBaseFile(string fullpath, XElement element)
        {
            if (!Directory.Exists(Path.GetDirectoryName(fullpath)))
                Directory.CreateDirectory(Path.GetDirectoryName(fullpath));

            if (System.IO.File.Exists(fullpath))
                System.IO.File.Delete(fullpath);

            element.Save(fullpath);
            return true; 
        }

        public static void ArchiveFile(string path, IContentBase node, bool media = false)
        {
            LogHelper.Debug<FileHelper>("Archiving. {0} {1}", ()=> path, ()=> node.Name);

            string _root = _mappedRoot;
            string _ext = ".content";
            if (media)
            {
                LogHelper.Debug<FileHelper>("Archiving a Media Item");
                _root = _mappedMediaRoot;
                _ext = ".media";
            }

            string filename = string.Format("{0}{1}", CleanFileName(node.Name), _ext);
            string fullpath = Path.Combine(string.Format("{0}{1}", _root, path), filename);

            if ( System.IO.File.Exists(fullpath) )
            {
                if ( uSyncContentSettings.Versions ) 
                {
                    string archiveFolder = Path.Combine(string.Format("{0}{1}", _mappedArchive, path));
                    if (!Directory.Exists(archiveFolder))
                        Directory.CreateDirectory(archiveFolder);

                    string archiveFile = Path.Combine(archiveFolder, string.Format("{0}_{1}{2}", CleanFileName(node.Name), DateTime.Now.ToString("ddMMyy_HHmmss"),_ext));

                    System.IO.File.Copy(fullpath, archiveFile); 
                                     
                }
                System.IO.File.Delete(fullpath); 
            }
        }
         

        /// <summary>
        ///  moves media and content files, when their parent id has changed
        /// </summary>
        public static void MoveFile(string oldPath, string newPath, string name, bool media = false)
        {
            LogHelper.Debug<FileHelper>("Move\n Old: {0}\n New: {1}\n Name: {2}", () => oldPath, () => newPath, () => name);

            string _root = _mappedRoot;
            string _ext = ".content";
            if (media) 
            {
                LogHelper.Debug<FileHelper>("Moving a Media Item");
                _root = _mappedMediaRoot;
                _ext = ".media";
            }

            string oldRoot = String.Format("{0}{1}", _root, oldPath);
            string newFolder = String.Format("{0}{1}", _root, newPath);

            string oldFile = Path.Combine(oldRoot, String.Format("{0}{1}", CleanFileName(name), _ext));

            
            if (System.IO.File.Exists(oldFile))
                System.IO.File.Delete(oldFile);
            
            string oldFolder = Path.Combine(oldRoot, CleanFileName(name));
            
            if (Directory.Exists(oldFolder))
            {
                if (!Directory.Exists(Path.GetDirectoryName(newFolder)))
                    Directory.CreateDirectory(Path.GetDirectoryName(newFolder));

                Directory.Move(oldFolder, newFolder);
            }
        }

        public static void RenameFile(string path, IContentBase node, string oldName)
        {
            LogHelper.Info<FileHelper>("Rename {0} {1} {2}", () => path, ()=> oldName, ()=> node.GetType().Name);

            string _root = _mappedRoot ; 
            string _ext = ".content"; 
            if (node.GetType() == typeof(Media))
            {
                LogHelper.Info<FileHelper>("Renaming a Media Item"); 
                _root = _mappedMediaRoot;
                _ext = ".media"; 
            }

            string folderRoot = String.Format("{0}{1}", _root, path);

            string oldFile = Path.Combine(folderRoot,
                String.Format("{0}{1}", CleanFileName(oldName), _ext));

            if (System.IO.File.Exists(oldFile))
                System.IO.File.Delete(oldFile);

            string oldFolder = Path.Combine(folderRoot, CleanFileName(oldName));
            string newFolder = Path.Combine(folderRoot, CleanFileName(node.Name));

            if (Directory.Exists(oldFolder))
                Directory.Move(oldFolder, newFolder);

        }

        const string validString = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ" ; 

        public static string CleanFileName(string name)
        {
            // umbraco extension stips numbers from start - so a folder 2013 - has no name
            // return name.ToSafeAliasWithForcingCheck();
            //
            
            // A better scrub (Probibly should just bite the bullet for this one?)
            // return name.ReplaceMany(Path.GetInvalidFileNameChars(), ' ').Replace(" ", "");
            // return clean.ReplaceMany(extras.ToCharArray(), Char.) ; 

            //
            // a valid scrubber - keeps some consistanct with umbraco core
            //
            StringBuilder sb = new StringBuilder(); 

            for(int i = 0; i < name.Length; i++ )
            {
                if ( validString.Contains(name[i].ToString()) )
                {
                    sb.Append(name[i]);
                }
            }
            return sb.ToString(); 
        }


        public static void ExportMediaFile(string path, Guid key) 
        {
            string fileroot = IOHelper.MapPath(_mappedFilesRoot) ; 

            string folder =  Path.Combine(fileroot, key.ToString() ) ; 
            string dest = Path.Combine( folder, Path.GetFileName(path) ) ; 
            string source = IOHelper.MapPath( string.Format("~{0}", path)) ;


            LogHelper.Info<FileHelper>("Attempting Export {0} to {1}", () => source, () => dest); 

            if ( System.IO.File.Exists(source))
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                if (System.IO.File.Exists(dest))
                    System.IO.File.Delete(dest); 

                System.IO.File.Copy(source,dest) ; 
            }

        }

        public static void ImportMediaFile(Guid guid, IMedia item)
        {
            string fileroot = IOHelper.MapPath(_mappedFilesRoot);
            string folder = Path.Combine(fileroot, guid.ToString());

            LogHelper.Debug<FileHelper>("Importing {0}", () => folder);


            if (!Directory.Exists(folder))
                return;

            foreach (var file in Directory.GetFiles(folder, "*.*"))
            {

                LogHelper.Debug<FileHelper>("Import {0}", () => file);

                string filename = Path.GetFileName(file);

                FileStream s = new FileStream(file, FileMode.Open);

                item.SetValue("umbracoFile", filename, s);

                s.Close(); 


            }
        }

        public static void CleanMediaFiles(IMedia item)
        {
            string fileRoot = IOHelper.MapPath(_mappedFilesRoot);
            string folder = Path.Combine(fileRoot, ImportPairs.GetSourceGuid(item.Key).ToString());
            
            try
            {

                if (Directory.Exists(folder))
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        System.IO.File.Delete(file);
                    }
                    Directory.Delete(folder);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error<FileHelper>("Couldn't clean files folder", ex);
            }
        }
    }
}
