using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using System.IO;
using System;
using System.Linq;

namespace ClonesSyncer
{
    internal class ClonesSyncerWindow : EditorWindow
    {
        private List<ClonedProject> _clonesList;
        private bool _includeAssets;
        private bool _includePackages;
        private bool _includeProjectSettings;
        private bool _includeUserSettings;
        private List<string> _exclusionPatterns;
        private Vector2 scrollPos;
        private ReorderableList _clonesEditorList;
        private ReorderableList _exclusionEditorList;
        private bool _parametersFoldout;
        private string _newExclusionPattern = "";
        private List<string> _foldersToCopy;
        private static List<string> _allFolders = new List<string> { "Assets", "Packages", "ProjectSettings", "UserSettings" };
        private static List<ClonedProjectPlatform> _platformMapping = new() {
            new("Current platform", null, null),
            new(BuildTarget.Android.ToString(), $"BuildSettings.{BuildTarget.Android.ToString()}@2x", "android"),
            new(BuildTarget.EmbeddedLinux.ToString(), $"BuildSettings.{BuildTarget.EmbeddedLinux.ToString()}@2x", "EmbeddedLinux"),
            new(BuildTarget.GameCoreXboxOne.ToString(), $"BuildSettings.{BuildTarget.GameCoreXboxOne.ToString()}@2x", "GameCoreXboxOne"),
            new(BuildTarget.GameCoreXboxSeries.ToString(), $"BuildSettings.{BuildTarget.GameCoreXboxSeries.ToString()}@2x", "GameCoreXboxSeries"),
            new(BuildTarget.iOS.ToString(), $"BuildSettings.iPhone@2x", "iOS"),
            new("LinuxHeadlessSimulation", $"BuildSettings.LinuxHeadlessSimulation@2x", "LinuxHeadlessSimulation"),
            new(BuildTarget.PS4.ToString(), $"BuildSettings.{BuildTarget.PS4.ToString()}@2x", "PS4"),
            new(BuildTarget.PS5.ToString(), $"BuildSettings.{BuildTarget.PS5.ToString()}@2x", "PS5"),
            new(BuildTarget.QNX.ToString(), $"BuildSettings.{BuildTarget.QNX.ToString()}@2x", "QNX"),
            new(BuildTarget.Stadia.ToString(), $"BuildSettings.{BuildTarget.Stadia.ToString()}@2x", "Stadia"),
            new(BuildTarget.StandaloneLinux64.ToString(), $"BuildSettings.Standalone@2x", "StandaloneLinux64"),
            new("Dedicated server Linux64", $"BuildSettings.DedicatedServer@2x", "StandaloneLinux64:Server"),
            new(BuildTarget.StandaloneOSX.ToString(), $"BuildSettings.Standalone@2x", "StandaloneOSX"),
            new("Dedicated server OSX", $"BuildSettings.DedicatedServer@2x", "StandaloneOSX:Server"),
            new(BuildTarget.StandaloneWindows.ToString(), $"BuildSettings.Standalone@2x", "StandaloneWindows"),
            new("Dedicated server Windows", $"BuildSettings.DedicatedServer@2x", "StandaloneWindows:Server"),
            new(BuildTarget.StandaloneWindows64.ToString(), $"BuildSettings.Standalone@2x", "StandaloneWindows64"),
            new("Dedicated server Windows64", $"BuildSettings.DedicatedServer@2x", "StandaloneWindows64:Server"),
            new(BuildTarget.Switch.ToString(), $"BuildSettings.{BuildTarget.Switch.ToString()}@2x", "Switch"),
            new(BuildTarget.tvOS.ToString(), $"BuildSettings.{BuildTarget.tvOS.ToString()}@2x", "tvOS"),
            new(BuildTarget.VisionOS.ToString(), $"BuildSettings.{BuildTarget.VisionOS.ToString()}@2x", "VisionOS"),
            new(BuildTarget.WebGL.ToString(), $"BuildSettings.{BuildTarget.WebGL.ToString()}@2x", "WebGL"),
            new(BuildTarget.WSAPlayer.ToString(), $"BuildSettings.Metro@2x", "WSAPlayer"),
            new(BuildTarget.XboxOne.ToString(), $"BuildSettings.{BuildTarget.XboxOne.ToString()}@2x", "XboxOne"),
        };
        [MenuItem("Window/Clones Syncer")]
        internal static void ShowWindow()
        {
            GetWindow<ClonesSyncerWindow>("Clones Syncer").minSize = Vector2.zero;
        }

        private void OnEnable()
        {
            _clonesList = ClonesSyncerSettingsManager.GetClones();
            _includeAssets = ClonesSyncerSettingsManager.GetIncludeAssets();
            _includePackages = ClonesSyncerSettingsManager.GetIncludePackages();
            _includeProjectSettings = ClonesSyncerSettingsManager.GetIncludeProjectSettings();
            _includeUserSettings = ClonesSyncerSettingsManager.GetIncludeUserSettings();
            _exclusionPatterns = ClonesSyncerSettingsManager.GetExclusionPatterns();
            RefreshSubfoldersList();
            _clonesEditorList = new(_clonesList, _clonesList.GetType(), true, false, true, true);
            _clonesEditorList.elementHeight = 55f;
            _clonesEditorList.drawElementCallback += DrawCloneElementCallback;
            _clonesEditorList.onAddCallback += OnAddCloneCallback;
            _clonesEditorList.onRemoveCallback += OnRemoveCloneCallback;
            _exclusionEditorList = new(_exclusionPatterns, _exclusionPatterns.GetType(), true, false, false, true);

        }
        private void OnRemoveCloneCallback(ReorderableList list)
        {
            if (list.count == 0)
            {
                return;
            }
            if (list.index == -1)
            {
                list.index = list.count - 1;
            }
            _clonesList.RemoveAt(list.index);
            SaveClones();
            EditorUtility.DisplayDialog("Clone removed", "The clone project has been removed from the list but the directory and its content still exists.\nIt is your responsability to delete it or not.", "Ok");
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Clones", new GUIStyle(EditorStyles.boldLabel), GUILayout.Height(27.5f));
            if (_clonesList.Count > 1)
            {
                if (GUILayout.Button(new GUIContent(" Synchronize all", EditorGUIUtility.IconContent("d_RotateTool On@2x").image, "Synchronize all the cloned projects on the list"), GUILayout.Width(150f), GUILayout.Height(27.5f)))
                {
                    SynchronizeAll();
                }
                EditorGUILayout.LabelField("", GUILayout.Width(1f), GUILayout.Height(0f));
            }
            EditorGUILayout.EndHorizontal();
            _clonesEditorList.DoLayoutList();
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
                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("Exclusion Patterns:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                _newExclusionPattern = EditorGUILayout.TextField(new GUIContent("Add an exclusion pattern"), _newExclusionPattern);
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_newExclusionPattern) || _exclusionPatterns.Contains(_newExclusionPattern));
                if (GUILayout.Button(EditorGUIUtility.IconContent("CreateAddNew")))
                {
                    _exclusionPatterns.Add(_newExclusionPattern);
                    _newExclusionPattern = "";
                    _exclusionEditorList.index = _exclusionEditorList.count - 1;
                    EditorGUI.FocusTextInControl("");
                    SaveExclusionPatterns();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUI.BeginChangeCheck();
                _exclusionEditorList.DoLayoutList();
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
            if (_clonesList.FindIndex(x => x.path == selectedPath) > -1)
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

            if (EditorUtility.DisplayDialog("Confirm", $"Do you want to clone the project to:\n{targetPath}?\nThis will synchronize the " + string.Join(", ", _allFolders) + " directories at the selected path.", "Yes", "No"))
            {
                EditorUtility.DisplayProgressBar("Cloning project", "Please wait...", 0f);
                if (OperateClone(targetPath, true))
                {
                    _clonesList.Add(new() { path = targetPath, platform = new("Current platform", null, null) });
                    SaveClones();
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Project cloned successfully.");
                }
                else
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Project cloning has failed. See console for details.");
                }
            }
        }
        private void SynchronizeClone(int cloneIndex)
        {
            string targetPath = _clonesList[cloneIndex].path;
            EditorUtility.DisplayProgressBar("Synchronizing project", "Please wait...", 0f);
            if (OperateClone(targetPath))
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project synchronized successfully.");
            }
            else
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project synchronization has failed. See console for details.");
            }
        }
        private void SynchronizeAll()
        {
            EditorUtility.DisplayProgressBar("Synchronizing project", "Please wait...", 0f);
            List<int> failIndexes = new();
            for (int i = 0; i < _clonesList.Count; i++)
            {
                if (!OperateClone(_clonesList[i].path))
                {
                    failIndexes.Add(i);
                }
            }
            if (failIndexes.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project(s) synchronized successfully.");
            }
            else
            {
                EditorUtility.ClearProgressBar();
                Debug.Log("Project(s) synchronization has failed for the following clones:" + failIndexes.Select(x => "\n" + _clonesList[x]) + "\nSee console for details.");
            }
        }

        private bool OperateClone(string targetPath, bool forceAllFolders = false)
        {
            try
            {
                List<string> foldersToCopy = forceAllFolders ? _allFolders : _foldersToCopy;
                foreach (string folder in foldersToCopy)
                {
                    string sourcePath = Path.Combine(Application.dataPath, "../", folder);
                    string destinationPath = Path.Combine(targetPath, folder);

                    SynchronizeDirectories(sourcePath, destinationPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                return false;
            }
        }

        private void SynchronizeDirectories(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            var sourceFiles = Directory.GetFiles(sourceDir);
            var targetFiles = Directory.GetFiles(targetDir);

            var targetFileSet = new HashSet<string>(targetFiles.Select(f => Path.GetFileName(f)));

            foreach (string filePath in sourceFiles)
            {
                string fileName = Path.GetFileName(filePath);
                if (_exclusionPatterns.Any(pattern => fileName.Contains(pattern.Trim()))) continue;

                string targetFilePath = Path.Combine(targetDir, fileName);

                if (!targetFileSet.Contains(fileName) || IsFileDifferent(filePath, targetFilePath))
                {
                    try
                    {
                        File.Copy(filePath, targetFilePath, true);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        throw new Exception("This file access has been denied : " + targetFilePath + "\nTry to manually delete protected folders (like the '.git' hidden folder) and to add them into the exclusion pattern list on the parameters.\nOriginal error :\n" + e);
                    }
                }

                targetFileSet.Remove(fileName);
            }

            foreach (var obsoleteFile in targetFileSet)
            {
                string deletePath = Path.Combine(targetDir, obsoleteFile);
                try
                {
                    File.Delete(deletePath);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new Exception("This file access has been denied : " + deletePath + "\nTry to manually delete protected folders (like the '.git' hidden folder) and to add them into the exclusion pattern list on the parameters.\nOriginal error :\n" + e);
                }
            }

            var sourceDirs = Directory.GetDirectories(sourceDir);
            var targetDirs = Directory.GetDirectories(targetDir);

            var targetDirSet = new HashSet<string>(targetDirs.Select(d => Path.GetFileName(d)));

            foreach (string dirPath in sourceDirs)
            {
                string dirName = Path.GetFileName(dirPath);
                if (_exclusionPatterns.Any(pattern => dirName.Contains(pattern.Trim()))) continue;

                string targetSubDir = Path.Combine(targetDir, dirName);

                SynchronizeDirectories(dirPath, targetSubDir);

                targetDirSet.Remove(dirName);
            }

            foreach (var obsoleteDir in targetDirSet)
            {
                string deletePath = Path.Combine(targetDir, obsoleteDir);
                try
                {
                    Directory.Delete(deletePath, true);
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new Exception("This file access has been denied : " + deletePath + "\nTry to manually delete protected folders (like the '.git' hidden folder) and to add them into the exclusion pattern list on the parameters.\nOriginal error :\n" + e);
                }
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
        private void OnAddCloneCallback(ReorderableList list)
        {
            AddClone();
        }

        private void DrawCloneElementCallback(Rect position, int index, bool isActive, bool isFocused)
        {
            position.height -= 5f;
            position.y += 2.5f;
            Rect rect = position;
            rect.width = position.width - 150f;
            rect.height = rect.height * 0.5f;
            EditorGUI.LabelField(rect, Path.GetFileName(_clonesList[index].path), EditorStyles.boldLabel);
            rect.y += rect.height;
            EditorGUI.LabelField(rect, _clonesList[index].path);
            rect.y = position.y;
            rect.x = rect.x + rect.width;
            rect.width = 150f;
            if (GUI.Button(rect, new GUIContent(" Synchronize", EditorGUIUtility.IconContent("Refresh@2x").image, "Synchronize project")))
            {
                SynchronizeClone(index);
            }
            rect.y += rect.height;
            rect.width = 120f;
            if (GUI.Button(rect, new GUIContent("Open clone", EditorGUIUtility.IconContent("d_UnityLogo").image, "Open clone project with selected platform")))
            {
                OpenProjectInUnity(_clonesList[index].path, _clonesList[index].platform.buildTarget);
            }
            rect.x += rect.width;
            rect.width = 30f;
            int selectedIndex = _platformMapping.FindIndex(x => x.label == _clonesList[index].platform.label);

            if (GUI.Button(rect, new GUIContent(EditorGUIUtility.IconContent(string.IsNullOrEmpty(_clonesList[index].platform.iconPath) ? "d_icon dropdown" : _clonesList[index].platform.iconPath).image, "Select specific platform")))
            {
                GenericMenu menu = new();
                for (int i = 0; i < _platformMapping.Count; i++)
                {
                    menu.AddItem(new GUIContent(_platformMapping[i].label), _platformMapping[i].label == _clonesList[index].platform.label, (object platform) =>
                    {
                        _clonesList[index].platform = (ClonedProjectPlatform)platform;
                        SaveClones();
                    }, _platformMapping[i]);
                }
                menu.ShowAsContext();
            }
        }
        private void OpenProjectInUnity(string projectPath, string buildTargetPlatform)
        {
            string unityPath = EditorApplication.applicationPath;
            bool headless = buildTargetPlatform.Contains(":Server");
            if (headless)
            {
                buildTargetPlatform = buildTargetPlatform.Substring(0, buildTargetPlatform.IndexOf(":Server"));
                Debug.Log(buildTargetPlatform);
                return;
            }
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
                    Arguments = $"-projectPath \"{projectPath}\"" + (string.IsNullOrEmpty(buildTargetPlatform) ? "" : $" -buildTarget {buildTargetPlatform}"),
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
        private void SaveClones()
        {
            ClonesSyncerSettingsManager.SetClones(_clonesList);
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