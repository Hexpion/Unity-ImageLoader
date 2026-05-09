using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Extensions.Unity.ImageLoader.Editor
{
    /// <summary>
    /// Editor window that generates low-resolution PNG versions of selected <see cref="Texture2D"/>
    /// assets.  Access it via <b>Assets → ImageLoader → Generate Low-Res Version</b> from the
    /// Project window context menu.
    /// </summary>
    public class TextureResizerEditorWindow : EditorWindow
    {
        private int    targetWidth          = 128;
        private int    targetHeight         = 128;
        private string outputFolder         = "";
        private string namingSuffix         = "_lowres";
        private bool   addAddressablesLabel = false;

        // ─────────────────────────────────────────────────────────────────────────

        [MenuItem("Assets/ImageLoader/Generate Low-Res Version")]
        private static void ShowWindow()
        {
            var window = GetWindow<TextureResizerEditorWindow>("Generate Low-Res Textures");
            window.minSize = new Vector2(340, 240);

            // Pre-fill output folder from the first selected texture.
            var selected = Selection.activeObject as Texture2D;
            if (selected != null)
            {
                var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(selected));
                if (!string.IsNullOrEmpty(dir))
                    window.outputFolder = dir.Replace('\\', '/');
            }

            window.Show();
        }

        [MenuItem("Assets/ImageLoader/Generate Low-Res Version", validate = true)]
        private static bool ValidateShowWindow()
        {
            foreach (var obj in Selection.objects)
                if (obj is Texture2D)
                    return true;
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            GUILayout.Label("Low-Res Texture Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetWidth  = EditorGUILayout.IntField("Max Width",   targetWidth);
            targetHeight = EditorGUILayout.IntField("Max Height",  targetHeight);
            namingSuffix = EditorGUILayout.TextField("Name Suffix", namingSuffix);

            EditorGUILayout.Space();
            GUILayout.Label("Output Folder (relative to project root)");
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField(outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(64)))
            {
                var chosen = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    var dataPath = Application.dataPath;
                    if (chosen.StartsWith(dataPath))
                        outputFolder = ("Assets" + chosen.Substring(dataPath.Length)).Replace('\\', '/');
                    else
                        outputFolder = chosen;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            addAddressablesLabel = EditorGUILayout.Toggle(
                new GUIContent("Add 'ImageLoader_LowRes' label",
                               "Marks generated textures with the 'ImageLoader_LowRes' asset label " +
                               "for easy grouping in Addressables."),
                addAddressablesLabel);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(outputFolder)))
            {
                if (GUILayout.Button("Generate"))
                    GenerateForSelection();
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
                EditorGUILayout.HelpBox("Please specify an output folder.", MessageType.Warning);
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void GenerateForSelection()
        {
            int count   = 0;
            int skipped = 0;

            foreach (var obj in Selection.objects)
            {
                if (!(obj is Texture2D)) continue;

                var sourcePath = AssetDatabase.GetAssetPath(obj);
                var absSource  = Path.Combine(Directory.GetCurrentDirectory(), sourcePath);

                if (!File.Exists(absSource))
                {
                    Debug.LogWarning($"[ImageLoader] TextureResizer: source file not found at '{absSource}'. Skipping.");
                    skipped++;
                    continue;
                }

                var sourceBytes = File.ReadAllBytes(absSource);
                var resizedPng  = TextureResizer.ResizeBytes(sourceBytes, targetWidth, targetHeight);

                if (resizedPng == null)
                {
                    Debug.LogWarning($"[ImageLoader] TextureResizer: could not resize '{sourcePath}'. Skipping.");
                    skipped++;
                    continue;
                }

                var outFileName = Path.GetFileNameWithoutExtension(sourcePath) + namingSuffix + ".png";
                var outRelPath  = Path.Combine(outputFolder, outFileName).Replace('\\', '/');
                var outAbsPath  = Path.Combine(Directory.GetCurrentDirectory(), outRelPath);

                Directory.CreateDirectory(Path.GetDirectoryName(outAbsPath));
                File.WriteAllBytes(outAbsPath, resizedPng);
                AssetDatabase.ImportAsset(outRelPath, ImportAssetOptions.ForceUpdate);

                if (addAddressablesLabel)
                    AddLabel(outRelPath, "ImageLoader_LowRes");

                Debug.Log($"[ImageLoader] Generated low-res texture: {outRelPath}");
                count++;
            }

            AssetDatabase.Refresh();

            var msg = count > 0
                ? $"Generated {count} low-res texture(s)."
                : "No textures were generated.";
            if (skipped > 0)
                msg += $"\n{skipped} asset(s) were skipped (see Console for details).";

            EditorUtility.DisplayDialog("Done", msg, "OK");
        }

        private static void AddLabel(string assetPath, string label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (asset == null) return;

            var existing = AssetDatabase.GetLabels(asset);
            var labels   = new List<string>(existing);
            if (!labels.Contains(label))
            {
                labels.Add(label);
                AssetDatabase.SetLabels(asset, labels.ToArray());
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Provides a batch-generation tool accessible from the top menu:
    /// <b>Tools → ImageLoader → Batch Generate Low-Res Textures</b>.
    /// </summary>
    public static class TextureResizerBatchMenu
    {
        [MenuItem("Tools/ImageLoader/Batch Generate Low-Res Textures")]
        private static void ShowBatchWindow()
        {
            EditorWindow.GetWindow<TextureResizerEditorWindow>("Batch Low-Res Generator").Show();
        }
    }
}
