using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SettingsManagement;

namespace ClonesSyncer
{
    static class ClonesSyncerSettingsManager
    {
        internal const string k_PackageName = "com.kevincastejon.clones-syncer";

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


        public static void Save()
        {
            instance.Save();
        }

        public static List<string> GetClones()
        {
            return instance.Get("clones", SettingsScope.Project, new List<string>());
        }

        public static void SetClones(List<string> value)
        {
            instance.Set("clones", value, SettingsScope.Project);
        }
        public static bool GetIncludeAssets()
        {
            return instance.Get("includeAssets", SettingsScope.Project, true);
        }

        public static void SetIncludeAssets(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includeAssets", value, scope);
        }
        public static bool GetIncludePackages()
        {
            return instance.Get("includePackages", SettingsScope.Project, true);
        }

        public static void SetIncludePackages(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includePackages", value, scope);
        }
        public static bool GetIncludeProjectSettings()
        {
            return instance.Get("includeProjectSettings", SettingsScope.Project, true);
        }

        public static void SetIncludeProjectSettings(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includeProjectSettings", value, scope);
        }
        public static bool GetIncludeUserSettings()
        {
            return instance.Get("includeUserSettings", SettingsScope.Project, false);
        }

        public static void SetIncludeUserSettings(bool value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("includeUserSettings", value, scope);
        }
        public static string GetExclusionPatterns()
        {
            return instance.Get("exclusionPatterns", SettingsScope.Project, ".git");
        }

        public static void SetExclusionPatterns(string value, SettingsScope scope = SettingsScope.Project)
        {
            instance.Set("exclusionPatterns", value, scope);
        }


    }
}
