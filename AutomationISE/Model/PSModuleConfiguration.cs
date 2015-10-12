﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web.Script.Serialization;

using System.Diagnostics;

namespace AutomationISE.Model
{
    public class PSModuleConfiguration
    {
        /* Updates the AzureAutomationAuthoringToolkit PowerShell Module to point at the specified workspace directory */
        public static void UpdateModuleConfiguration(string workspace)
        {
            string modulePath = findModulePath();
            string configFilePath = System.IO.Path.Combine(modulePath, ModuleData.ConfigFileName);

            if (!File.Exists(configFilePath))
            {
                Debug.WriteLine("Warning: a config file wasn't found in the module, so a new one will be created");
            }

            JavaScriptSerializer jss = new JavaScriptSerializer();
            List<PSModuleConfigurationItem> config = jss.Deserialize<List<PSModuleConfigurationItem>>((File.ReadAllText(configFilePath)));

            foreach (PSModuleConfigurationItem pc in config)
            {
                if (pc.Name.Equals(ModuleData.LocalAssetsPath_FieldName))
                {
                    pc.Value = System.IO.Path.Combine(workspace, ModuleData.LocalAssetsFileName);
                }
                else if (pc.Name.Equals(ModuleData.SecureLocalAssetsPath_FieldName))
                {
                    pc.Value = System.IO.Path.Combine(workspace, ModuleData.SecureLocalAssetsFileName);
                }
                else if (pc.Name.Equals(ModuleData.EncryptionCertificateThumbprint_FieldName))
                {
                    //   not setting thumbprint
                }
                else
                {
                    Debug.WriteLine("Unknown configuration found: " + pc.Name);
                }
            }

            File.WriteAllText(configFilePath, jss.Serialize(config)); // TODO: use a friendly JSON formatter for serialization
        }

        public static string findModulePath()
        {
            String[] moduleLocations = Environment.GetEnvironmentVariable(ModuleData.EnvPSModulePath).Split(';');
            foreach (String moduleLocation in moduleLocations)
            {
                String possibleModulePath = System.IO.Path.Combine(moduleLocation, ModuleData.ModuleName);
                if (Directory.Exists(possibleModulePath))
                {
                    if(!File.Exists(System.IO.Path.Combine(possibleModulePath, ModuleData.ConfigFileName))) 
                    {
                        // config file for module is not directly under module folder -- module contents is probably under a PSv5 version
                        // folder under module folder, so return highest version folder path
                        var versionFolders = Directory.EnumerateDirectories(possibleModulePath);
                        return versionFolders.ElementAt(versionFolders.Count() - 1);
                    }
                    else
                    {
                        // config file for module is directly under module folder -- module contents is directly under module folder
                        return possibleModulePath;
                    }
                }
            }
            return null;
        }

        public class PSModuleConfigurationItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public static class ModuleData
        {
            public const string ModuleName = "AzureAutomationAuthoringToolkit";
            public const string ConfigFileName = "Config.json";
            public const string LocalAssetsPath_FieldName = "LocalAssetsPath";
            public const string SecureLocalAssetsPath_FieldName = "SecureLocalAssetsPath";
            public const string EncryptionCertificateThumbprint_FieldName = "EncryptionCertificateThumbprint";
            public const string LocalAssetsFileName = "LocalAssets.json";
            public const string SecureLocalAssetsFileName = "SecureLocalAssets.json";
            public const string EnvPSModulePath = "PSModulePath";
        }
    }
}
