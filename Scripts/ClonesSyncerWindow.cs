using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using System.IO;
using System;
using System.Linq;

namespace ClonesSyncer
{
    public class ClonesSyncerWindow : EditorWindow
    {
        private class ClonedProjectPlatform
        {
            public string label;
            public string buildTarget;
        }
        private List<string> _list;
        private bool _includeAssets;
        private bool _includePackages;
        private bool _includeProjectSettings;
        private bool _includeUserSettings;
        private string _exclusionPatterns;
        private Vector2 scrollPos;
        private ReorderableList _editorList;
        private bool _operating;
        private bool _parametersFoldout;
        private List<string> _foldersToCopy;
        private static List<string> _allFolders = new List<string> { "Assets", "Packages", "ProjectSettings", "UserSettings" };

        private static ClonedProjectPlatform[] _platforms = new ClonedProjectPlatform[] {
#if UNITY_STANDALONE_WIN
            new (){ label = "Standalone", buildTarget = "StandaloneWindows64" },
#elif UNITY_STANDALONE_OSX
            new (){ label = "Standalone", buildTarget = "StandaloneOSX" },
#elif UNITY_STANDALONE_LINUX
            new (){ label = "Standalone", buildTarget = "StandaloneLinux" },
#endif
            new (){ label = "Android", buildTarget = "Android" },
            new (){ label = "WebGL", buildTarget = "WebGL" },
        };

        [MenuItem("Window/Cloned Projects Synchronizer")]
        public static void ShowWindow()
        {
            GetWindow<ClonesSyncerWindow>("Cloned Projects Synchronizer");
        }

        private void OnEnable()
        {
            _list = ClonesSyncerSettingsManager.GetClones();
            _includeAssets = ClonesSyncerSettingsManager.GetIncludeAssets();
            _includePackages = ClonesSyncerSettingsManager.GetIncludePackages();
            _includeProjectSettings = ClonesSyncerSettingsManager.GetIncludeProjectSettings();
            _includeUserSettings = ClonesSyncerSettingsManager.GetIncludeUserSettings();
            _exclusionPatterns = ClonesSyncerSettingsManager.GetExclusionPatterns();
            RefreshSubfoldersList();
            _editorList = new(_list, _list.GetType(), true, true, true, true);
            _editorList.elementHeight = 55f;
            _editorList.drawElementCallback += DrawElementCallback;
            _editorList.onAddCallback += OnAddCallback;
            _editorList.onRemoveCallback += OnRemoveCallback;
            _editorList.drawHeaderCallback += DrawHeaderCallback;
        }

        private void OnRemoveCallback(ReorderableList list)
        {
            if (list.count == 0)
            {
                return;
            }
            if (list.index == -1)
            {
                list.index = list.count - 1;
            }
            _list.RemoveAt(list.index);
            SaveClones();
            EditorUtility.DisplayDialog("Clone removed", "The folder and files are not deleted, you can remove them yourself at your will.", "Ok");
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUI.BeginDisabledGroup(_operating);
            _editorList.DoLayoutList();
            EditorGUI.EndDisabledGroup();
            DrawParameters();
            EditorGUILayout.EndScrollView();
        }

        private void DrawParameters()
        {
            _parametersFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_parametersFoldout, "Parameters");
            if (_parametersFoldout)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Synchronized folders", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("(Note that when creating a clone all four folders will always be sychronized)", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _includeAssets = EditorGUILayout.ToggleLeft("Include Assets", _includeAssets, GUILayout.Width(130));
                if (EditorGUI.EndChangeCheck())
                {
                    SaveIncludeAssets();
                    RefreshSubfoldersList();
                }
                EditorGUI.BeginChangeCheck();
                _includeProjectSettings = EditorGUILayout.ToggleLeft("Include ProjectSettings", _includeProjectSettings, GUILayout.Width(160));
                if (EditorGUI.EndChangeCheck())
                {
                    SaveIncludeProjectSettings();
                    RefreshSubfoldersList();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _includePackages = EditorGUILayout.ToggleLeft("Include Packages", _includePackages, GUILayout.Width(130));
                if (EditorGUI.EndChangeCheck())
                {
                    SaveIncludePackages();
                    RefreshSubfoldersList();
                }
                EditorGUI.BeginChangeCheck();
                _includeUserSettings = EditorGUILayout.ToggleLeft("Include UserSettings", _includeUserSettings, GUILayout.Width(160));
                if (EditorGUI.EndChangeCheck())
                {
                    SaveIncludeUserSettings();
                    RefreshSubfoldersList();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Exclusion Patterns (comma-separated):", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _exclusionPatterns = EditorGUILayout.TextField(_exclusionPatterns);
                if (EditorGUI.EndChangeCheck())
                {
                    SaveExclusionPatterns();
                }
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }
        }

        private bool SelectCloneFolder(out string targetPath)
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select clone project root folder", "", "");

            if (string.IsNullOrEmpty(selectedPath))
            {
                targetPath = null;
                return false;
            }

            if (Path.GetFullPath(Path.Combine(Application.dataPath, "..")) == Path.GetFullPath(selectedPath))
            {
                EditorUtility.DisplayDialog("Wrong project selected", "You cannot select the master project as a clone !", "Ok");
                targetPath = null;
                return false;
            }
            if (_list.Contains(selectedPath))
            {
                EditorUtility.DisplayDialog("Wrong project selected", "This project is already on the clones list !", "Ok");
                targetPath = null;
                return false;
            }
            targetPath = selectedPath;
            return true;
        }
        private void AddClone()
        {
            if (!SelectCloneFolder(out string targetPath)) { return; }

            if (EditorUtility.DisplayDialog("Confirm", $"Do you want to clone the project to:\n{targetPath}?\nThis will delete all the content currently located into the " + string.Join(", ", _allFolders) + " on the clone folder path, if existing.", "Yes", "No"))
            {
                _operating = true;
                EditorUtility.DisplayProgressBar("Cloning project", "Please wait...", 0f);
                if (OperateClone(targetPath, true))
                {
                    _list.Add(targetPath);
                    SaveClones();
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Project cloned successfully.");
                    _operating = false;
                }
                else
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Project cloning has failed. See console for details.");
                    _operating = false;
                }
            }
        }
        private void SynchronizeClone(int cloneIndex)
        {
            string targetPath = _list[cloneIndex];
            _operating = true;
            EditorUtility.DisplayProgressBar("Synchronizing project", "Please wait...", 0f);
            if (OperateClone(targetPath))
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project synchronized successfully.");
                _operating = false;
            }
            else
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project synchronization has failed. See console for details.");
                _operating = false;
            }
        }
        private void SynchronizeAll()
        {
            _operating = true;
            EditorUtility.DisplayProgressBar("Synchronizing project", "Please wait...", 0f);
            List<int> failIndexes = new();
            for (int i = 0; i < _list.Count; i++)
            {
                if (!OperateClone(_list[i]))
                {
                    failIndexes.Add(i);
                }
            }
            if (failIndexes.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project(s) synchronized successfully.");
                _operating = false;
            }
            else
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project(s) synchronization has failed for the following clones:" + failIndexes.Select(x => "\n" + _list[x]) + "\nSee console for details.");
                _operating = false;
            }
        }

        private bool OperateClone(string targetPath, bool forceAllFolders = false)
        {
            try
            {
                string[] exclusionArray = _exclusionPatterns.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> foldersToCopy = forceAllFolders ? _allFolders : _foldersToCopy;
                foreach (string folder in foldersToCopy)
                {
                    string sourcePath = Path.Combine(Application.dataPath, "../", folder);
                    string destinationPath = Path.Combine(targetPath, folder);

                    SynchronizeDirectories(sourcePath, destinationPath, exclusionArray);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                return false;
            }
        }

        private void SynchronizeDirectories(string sourceDir, string targetDir, string[] exclusionPatterns)
        {
            Directory.CreateDirectory(targetDir);

            var sourceFiles = Directory.GetFiles(sourceDir);
            var targetFiles = Directory.GetFiles(targetDir);

            var targetFileSet = new HashSet<string>(targetFiles.Select(f => Path.GetFileName(f)));

            foreach (string filePath in sourceFiles)
            {
                string fileName = Path.GetFileName(filePath);
                if (exclusionPatterns.Any(pattern => fileName.Contains(pattern.Trim()))) continue;

                string targetFilePath = Path.Combine(targetDir, fileName);

                if (!targetFileSet.Contains(fileName) || IsFileDifferent(filePath, targetFilePath))
                {
                    File.Copy(filePath, targetFilePath, true);
                }

                targetFileSet.Remove(fileName);
            }

            foreach (var obsoleteFile in targetFileSet)
            {
                File.Delete(Path.Combine(targetDir, obsoleteFile));
            }

            var sourceDirs = Directory.GetDirectories(sourceDir);
            var targetDirs = Directory.GetDirectories(targetDir);

            var targetDirSet = new HashSet<string>(targetDirs.Select(d => Path.GetFileName(d)));

            foreach (string dirPath in sourceDirs)
            {
                string dirName = Path.GetFileName(dirPath);
                if (exclusionPatterns.Any(pattern => dirName.Contains(pattern.Trim()))) continue;

                string targetSubDir = Path.Combine(targetDir, dirName);

                SynchronizeDirectories(dirPath, targetSubDir, exclusionPatterns);

                targetDirSet.Remove(dirName);
            }

            foreach (var obsoleteDir in targetDirSet)
            {
                Directory.Delete(Path.Combine(targetDir, obsoleteDir), true);
            }
        }

        private bool IsFileDifferent(string sourceFilePath, string targetFilePath)
        {
            if (!File.Exists(targetFilePath))
                return true;

            FileInfo sourceInfo = new FileInfo(sourceFilePath);
            FileInfo targetInfo = new FileInfo(targetFilePath);

            return sourceInfo.Length != targetInfo.Length || sourceInfo.LastWriteTimeUtc != targetInfo.LastWriteTimeUtc;
        }
        private void RefreshSubfoldersList()
        {
            _foldersToCopy = new List<string>();
            if (_includeAssets)
            {
                _foldersToCopy.Add("Assets");
            }
            if (_includePackages)
            {
                _foldersToCopy.Add("Packages");
            }
            if (_includeProjectSettings)
            {
                _foldersToCopy.Add("ProjectSettings");
            }
            if (_includeUserSettings)
            {
                _foldersToCopy.Add("UserSettings");
            }
        }
        private void OnAddCallback(ReorderableList list)
        {
            AddClone();
        }

        private void DrawElementCallback(Rect position, int index, bool isActive, bool isFocused)
        {
            position.height -= 5f;
            position.y += 2.5f;
            Rect rect = position;
            rect.width = position.width - 125f;
            rect.height = rect.height * 0.5f;
            EditorGUI.LabelField(rect, Path.GetFileName(_list[index]), EditorStyles.boldLabel);
            rect.y += rect.height;
            EditorGUI.LabelField(rect, _list[index]);
            rect.y = position.y;
            rect.x = rect.x + rect.width;
            rect.width = 125f;
            if (GUI.Button(rect, new GUIContent("Synchronize", EditorGUIUtility.IconContent("Refresh@2x").image, "Synchronize project")))
            {
                SynchronizeClone(index);
            }
            rect.y += rect.height;
            if (GUI.Button(rect, new GUIContent("Open in Unity", EditorGUIUtility.IconContent("d_UnityLogo").image, "Open in Unity")))
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Current platform", "Current platform"), false, () => { OpenProjectInUnity(_list[index]); });
                for (int i = 0; i < _platforms.Length; i++)
                {
                    menu.AddItem(new GUIContent(_platforms[i].label), false, (object platform) => { OpenProjectInUnity(_list[index], (ClonedProjectPlatform)platform); }, _platforms[i]);
                }
                menu.ShowAsContext();
            }
        }
        private void OpenProjectInUnity(string projectPath, ClonedProjectPlatform buildTargetPlatform = null)
        {
            string unityPath = EditorApplication.applicationPath;

            if (!Directory.Exists(projectPath))
            {
                EditorUtility.DisplayDialog("Project not found", "The selected project folder does not exist.", "OK");
                return;
            }

            try
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = unityPath,
                    Arguments = $"-projectPath \"{projectPath}\"" + (buildTargetPlatform == null ? "" : $" -buildTarget {buildTargetPlatform.buildTarget}"),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
                {
                    Debug.Log($"Opening Unity project at {projectPath} using Unity at {unityPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to open Unity project: {ex.Message}");
            }
        }
        private void DrawHeaderCallback(Rect position)
        {
            Rect rect = position;
            rect.width -= 140f;
            EditorGUI.LabelField(position, "Cloned projects");
            rect.x += rect.width;
            rect.width = 140f;
            if (_list.Count > 1 && GUI.Button(rect, new GUIContent(" Synchronize all", EditorGUIUtility.IconContent("d_RotateTool On@2x").image, "Synchronize all the cloned projects on the list")))
            {
                SynchronizeAll();
            }
        }
        private void SaveClones()
        {
            ClonesSyncerSettingsManager.SetClones(_list);
            ClonesSyncerSettingsManager.Save();
        }
        private void SaveIncludeAssets()
        {
            ClonesSyncerSettingsManager.SetIncludeAssets(_includeAssets);
            ClonesSyncerSettingsManager.Save();
        }
        private void SaveIncludePackages()
        {
            ClonesSyncerSettingsManager.SetIncludePackages(_includePackages);
            ClonesSyncerSettingsManager.Save();
        }
        private void SaveIncludeProjectSettings()
        {
            ClonesSyncerSettingsManager.SetIncludeProjectSettings(_includeProjectSettings);
            ClonesSyncerSettingsManager.Save();
        }
        private void SaveIncludeUserSettings()
        {
            ClonesSyncerSettingsManager.SetIncludeUserSettings(_includeUserSettings);
            ClonesSyncerSettingsManager.Save();
        }
        private void SaveExclusionPatterns()
        {
            ClonesSyncerSettingsManager.SetExclusionPatterns(_exclusionPatterns);
            ClonesSyncerSettingsManager.Save();
        }
    }
}