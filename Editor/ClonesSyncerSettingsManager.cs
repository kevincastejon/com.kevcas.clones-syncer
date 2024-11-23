using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SettingsManagement;
using UnityEngine;

namespace ClonesSyncer
{
    [Serializable]
    internal class ClonedProject
    {
        [SerializeField]
        internal string path;
        [SerializeField]
        internal ClonedProjectPlatform platform;
    }
    [Serializable]
    internal class ClonedProjectPlatform
    {
        [SerializeField]
        internal string label;
        [SerializeField]
        internal string iconPath;
        [SerializeField] 
        internal string buildTarget;

        internal ClonedProjectPlatform(string label, string iconPath, string buildTarget)
        {
            this.label = label;
            this.iconPath = iconPath;
            this.buildTarget = buildTarget;
        }
    }
    [Serializable]
    internal class ExclusionPattern
    {
        [SerializeField]
        internal string pattern;
        [SerializeField]
        internal bool isActive;
    }
    internal static class ClonesSyncerSettingsManager
    {
        internal const string k_PackageName = "com.kevcas.clones-syncer";

        static Settings s_Instance;

        internal static Settings instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new Settings(k_PackageName);

                return s_Instance;
            }
        }


        internal static void Save()
        {
            instance.Save();
        }
        internal static string GetMasterProjectPath()
        {
            return instance.Get("masterProjectPath", SettingsScope.Project, "");
        }

        internal static void SetMasterProjectPath(string value)
        {
            instance.Set("masterProjectPath", value, SettingsScope.Project);
        }
        internal static List<ClonedProject> GetClones()
        {
            return instance.Get("clones", SettingsScope.Project, new List<ClonedProject>());
        }

        internal static void SetClones(List<ClonedProject> value)
        {
            instance.Set("clones", value, SettingsScope.Project);
        }
        internal static bool GetIncludeAssets()
        {
            return instance.Get("includeAssets", SettingsScope.Project, true);
        }

        internal static void SetIncludeAssets(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includeAssets", value, scope);
        }
        internal static bool GetIncludePackages()
        {
            return instance.Get("includePackages", SettingsScope.Project, false);
        }

        internal static void SetIncludePackages(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includePackages", value, scope);
        }
        internal static bool GetIncludeProjectSettings()
        {
            return instance.Get("includeProjectSettings", SettingsScope.Project, false);
        }

        internal static void SetIncludeProjectSettings(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includeProjectSettings", value, scope); 
        }
        internal static bool GetIncludeUserSettings()
        {
            return instance.Get("includeUserSettings", SettingsScope.Project, false);
        }

        internal static void SetIncludeUserSettings(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includeUserSettings", value, scope);
        }
        internal static List<ExclusionPattern> GetExclusionPatterns()
        {
            return instance.Get("exclusionPatterns", SettingsScope.Project, new List<ExclusionPattern>() { new() { pattern = ".git", isActive = true } });
        }

        internal static void SetExclusionPatterns(List<ExclusionPattern> value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("exclusionPatterns", value, scope);
        }


    }
}
