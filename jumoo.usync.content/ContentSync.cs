﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;
using Umbraco.Core.Logging;

using Umbraco.Core.IO;
using System.IO; 

namespace jumoo.usync.content
{
    public class ContentSync : ApplicationEventHandler
    {
        private static bool _synced = false;
        private static object _oSync = new object();

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            if (!_synced)
            {
                lock (_oSync)
                {
                    if (!_synced)
                    {
                        // do first time stuff...
                        LogHelper.Info(typeof(ContentSync), "Initalizing uSync Content Edition");

                        if (!Directory.Exists(IOHelper.MapPath(uSyncContentSettings.Folder)) || uSyncContentSettings.Export)
                        {
                            ExportContent(); 
                        }

                        if (uSyncContentSettings.Import)
                        {
                            ImportContent(); 
                        }

                        if (uSyncContentSettings.Events != "Off")
                        {
                            AttachEvents(uSyncContentSettings.Events == "Publish");
                        }

                        LogHelper.Info(typeof(ContentSync), "uSync Content Edition Initilized"); 
                    }
                }
            }
        }

        public int ImportContent()
        {
            ContentImporter ci = new ContentImporter();

            // 1. import the content 
            int importCount = ci.ImportDiskContent(false);

            // 2. import again but try to map id's
            ci.ImportDiskContent(true);

            return importCount; 
        }

        public void ExportContent()
        {
            // do the content exoort
            ContentWalker cw = new ContentWalker();
            cw.WalkSite();    
        }

        public void AttachEvents(bool onPublish)
        {
            ContentEvents events = new ContentEvents();
            events.AttachEvents(onPublish); // on the save/delete.
        }
    }
}
