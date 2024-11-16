using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using System.IO;
using System;
using System.Linq;

namespace ClonedProjectsSynchronizer
{
    public class ClonedProjectsSynchronizerWindow : EditorWindow
    {
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

        [MenuItem("Window/Cloned Projects Synchronizer")]
        public static void ShowWindow()
        {
            GetWindow<ClonedProjectsSynchronizerWindow>("Cloned Projects Synchronizer");
        }

        private void OnEnable()
        {
            _list = ClonedProjectsSynchronizerSettingsManager.GetClones();
            _includeAssets = ClonedProjectsSynchronizerSettingsManager.GetIncludeAssets();
            _includePackages = ClonedProjectsSynchronizerSettingsManager.GetIncludePackages();
            _includeProjectSettings = ClonedProjectsSynchronizerSettingsManager.GetIncludeProjectSettings();
            _includeUserSettings = ClonedProjectsSynchronizerSettingsManager.GetIncludeUserSettings();
            _exclusionPatterns = ClonedProjectsSynchronizerSettingsManager.GetExclusionPatterns();
            RefreshSubfoldersList();
            _editorList = new(_list, _list.GetType(), true, true, true, false);
            _editorList.elementHeight = 50f;
            _editorList.drawElementCallback += DrawElementCallback;
            _editorList.onAddCallback += OnAddCallback;
            _editorList.drawHeaderCallback += DrawHeaderCallback;
        }


        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            _parametersFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_parametersFoldout, "Parameters");
            if (_parametersFoldout)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
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

                EditorGUILayout.LabelField("Exclusion Patterns (comma-separated):");
                EditorGUI.BeginChangeCheck();
                _exclusionPatterns = EditorGUILayout.TextField(_exclusionPatterns);
                if (EditorGUI.EndChangeCheck())
                {
                    SaveExclusionPatterns();
                }
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }
            EditorGUI.BeginDisabledGroup(_operating);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(" Synchronize all", EditorGUIUtility.IconContent("d_RotateTool On@2x").image, "efsef"), GUILayout.Height(50f), GUILayout.Width(140f)))
            {
                SynchronizeAll();
            }
            EditorGUILayout.EndHorizontal();
            _editorList.DoLayoutList();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();
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

            if (EditorUtility.DisplayDialog("Confirm", $"Do you want to clone the project to:\n{targetPath}?\nThis will delete all the content currently located into the " + string.Join(", ", _foldersToCopy) + " on the clone folder path, if existing.", "Yes", "No"))
            {
                _operating = true;
                EditorUtility.DisplayProgressBar("Cloning project", "Please wait...", 0f);
                if (OperateClone(targetPath))
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

        private bool OperateClone(string targetPath)
        {
            try
            {
                string[] exclusionArray = _exclusionPatterns.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string folder in _foldersToCopy)
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
            Rect rect = position;
            rect.width = position.width - 150;
            rect.height = rect.height * 0.5f;
            EditorGUI.LabelField(rect, Path.GetFileName(_list[index]), EditorStyles.boldLabel);
            rect.y += rect.height;
            EditorGUI.LabelField(rect, _list[index]);
            rect.height = position.height;
            rect.y = position.y;
            rect.x = rect.width + 25f;
            rect.width = 50f;
            if (GUI.Button(rect, new GUIContent(EditorGUIUtility.IconContent("d_FolderOpened Icon", "Open folder"))))
            {
                EditorUtility.RevealInFinder(_list[index] + "/");
            }
            rect.x += 50f;
            if (GUI.Button(rect, EditorGUIUtility.IconContent("Refresh@2x")))
            {
                SynchronizeClone(index);
            }
            rect.x += 50f;
            if (GUI.Button(rect, EditorGUIUtility.IconContent("P4_DeletedLocal@2x")))
            {
                _list.RemoveAt(index);
                SaveClones();
                EditorUtility.DisplayDialog("Clone removed", "The folder and files are not deleted, you have to remove them yourself if you will.", "Ok");
            }
        }

        private void DrawHeaderCallback(Rect rect)
        {
            EditorGUI.LabelField(rect, "Cloned projects");
        }
        private void SaveClones()
        {
            ClonedProjectsSynchronizerSettingsManager.SetClones(_list);
            ClonedProjectsSynchronizerSettingsManager.Save();
        }
        private void SaveIncludeAssets()
        {
            ClonedProjectsSynchronizerSettingsManager.SetIncludeAssets(_includeAssets);
            ClonedProjectsSynchronizerSettingsManager.Save();
        }
        private void SaveIncludePackages()
        {
            ClonedProjectsSynchronizerSettingsManager.SetIncludePackages(_includePackages);
            ClonedProjectsSynchronizerSettingsManager.Save();
        }
        private void SaveIncludeProjectSettings()
        {
            ClonedProjectsSynchronizerSettingsManager.SetIncludeProjectSettings(_includeProjectSettings);
            ClonedProjectsSynchronizerSettingsManager.Save();
        }
        private void SaveIncludeUserSettings()
        {
            ClonedProjectsSynchronizerSettingsManager.SetIncludeUserSettings(_includeUserSettings);
            ClonedProjectsSynchronizerSettingsManager.Save();
        }
        private void SaveExclusionPatterns()
        {
            ClonedProjectsSynchronizerSettingsManager.SetExclusionPatterns(_exclusionPatterns);
            ClonedProjectsSynchronizerSettingsManager.Save();
        }
    }
}