﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Configuration; 

using Umbraco.Core.IO ; 
using Umbraco.Core.Logging ; 


namespace jumoo.usync.content
{
    public class uSyncContentSettingsSection : ConfigurationSection 
    {
        /// <summary>
        /// export content at startup
        /// </summary>
        [ConfigurationProperty("export", DefaultValue = true, IsRequired = false)]
        public Boolean Export
        {
            get { return (Boolean)this["export"]; }
        }
        
        /// <summary>
        /// import the content at startup
        /// </summary>
        [ConfigurationProperty("import", DefaultValue = true, IsRequired = false)]
        public Boolean Import
        {
            get { return (Boolean)this["import"]; }
        }

        /// <summary>
        /// attach to the events, save/delete etc and write when they happen
        /// 
        /// accepted values, are "save" and "publish", 
        /// with save, files will be written everysave
        /// with publish they are written when something is published
        /// </summary>
        [ConfigurationProperty("events", DefaultValue = "save", IsRequired = false)]
        public String Events
        {
            get { return (String)this["events"]; }
        }

        [ConfigurationProperty("folder", DefaultValue = "~/uSync/Content/", IsRequired = false)]
        public String Folder
        {
            get { return (String)this["folder"]; }
        }
    }

    public class uSyncContentSettings
    {
        private static string _settingsFile = "usyncContent.Config";
        private static uSyncContentSettingsSection _settings ; 
        
        static uSyncContentSettings()
        {
            try
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = IOHelper.MapPath(String.Format("~/config/{0}", _settingsFile));

                // load the settings
                Configuration config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

                _settings = (uSyncContentSettingsSection)config.GetSection("usync.content");
            }
            catch (Exception ex)
            {
                LogHelper.Error(typeof(uSyncContentSettings), "error loading settings file", ex);
            }
        }

        public static bool Export
        {
            get { return _settings.Export; }
        }

        public static bool Import
        {
            get { return _settings.Import; }
        }

        public static string Events
        {
            get { return _settings.Events; }
        }

        public static string Folder
        {
            get { return _settings.Folder; }
        }
    }   
}