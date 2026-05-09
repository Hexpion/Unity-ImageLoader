using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Extensions.Unity.ImageLoader.Editor
{
    /// <summary>
    /// Low-resolution texture generator built with UI Toolkit (UIElements).
    /// <para>Open via <b>Assets → ImageLoader → Generate Low-Res Version</b> (right-click
    /// on textures in the Project window) or <b>Tools → ImageLoader → Low-Res Generator</b>.</para>
    /// </summary>
    public class TextureResizerEditorWindow : EditorWindow
    {
        // ── Persistent state ──────────────────────────────────────────────
        private readonly List<Texture2D> _assets = new List<Texture2D>();
        private int    _maxWidth          = 128;
        private int    _maxHeight         = 128;
        private string _namingSuffix      = "_lowres";
        private string _outputFolder      = "";
        private string _folderPath        = "";
        private bool   _sameAsSource      = true;
        private bool   _recursive         = false;
        private bool   _addAddressLabel   = false;
        private int    _mode              = 0; // 0 = Selected Textures, 1 = Folder

        // ── UIElements references ─────────────────────────────────────────
        private ListView    _assetList;
        private Label       _statusLabel;
        private VisualElement _selectedPanel;
        private VisualElement _folderPanel;
        private VisualElement _outputFolderRow;

        // ─────────────────────────────────────────────────────────────────

        [MenuItem("Assets/ImageLoader/Generate Low-Res Version")]
        private static void ShowFromContextMenu()
        {
            var window = GetWindow<TextureResizerEditorWindow>("Low-Res Generator");
            window.minSize = new Vector2(380, 500);
            window.PopulateFromSelection();
            window.Show();
        }

        [MenuItem("Assets/ImageLoader/Generate Low-Res Version", validate = true)]
        private static bool ValidateContextMenu()
        {
            foreach (var obj in Selection.objects)
                if (obj is Texture2D)
                    return true;
            return false;
        }

        [MenuItem("Tools/ImageLoader/Low-Res Generator")]
        private static void ShowFromMenu()
        {
            var window = GetWindow<TextureResizerEditorWindow>("Low-Res Generator");
            window.minSize = new Vector2(380, 500);
            window.Show();
        }

        // ─────────────────────────────────────────────────────────────────

        private void PopulateFromSelection()
        {
            foreach (var obj in Selection.objects)
                if (obj is Texture2D tex && !_assets.Contains(tex))
                    _assets.Add(tex);

            if (_assets.Count > 0 && _sameAsSource)
            {
                var firstPath = AssetDatabase.GetAssetPath(_assets[0]);
                if (!string.IsNullOrEmpty(firstPath))
                    _outputFolder = Path.GetDirectoryName(firstPath).Replace('\\', '/');
            }
        }

        // ─────────────────────────────────────────────────────────────────

        public void CreateGUI()
        {
            var editorFolder = FindEditorScriptsFolder();

            // Load UXML
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{editorFolder}/TextureResizerEditor.uxml");
            if (uxml == null)
            {
                rootVisualElement.Add(new Label("[ImageLoader] Could not load TextureResizerEditor.uxml"));
                return;
            }
            uxml.CloneTree(rootVisualElement);

            // Load USS
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{editorFolder}/TextureResizerEditor.uss");
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);

            BindUI();
        }

        private void BindUI()
        {
            // ── Mode dropdown ──────────────────────────────────────────────
            var modeDropdown = rootVisualElement.Q<DropdownField>("mode-dropdown");
            modeDropdown.choices = new List<string> { "Selected Textures", "Folder" };
            modeDropdown.index   = _mode;
            modeDropdown.RegisterValueChangedCallback(evt =>
            {
                _mode = modeDropdown.index;
                RefreshPanels();
            });

            // ── Panels ────────────────────────────────────────────────────
            _selectedPanel   = rootVisualElement.Q<VisualElement>("selected-panel");
            _folderPanel     = rootVisualElement.Q<VisualElement>("folder-panel");
            _outputFolderRow = rootVisualElement.Q<VisualElement>("output-folder-row");

            // ── Asset list ─────────────────────────────────────────────────
            _assetList = rootVisualElement.Q<ListView>("asset-list");
            _assetList.itemsSource  = _assets;
            _assetList.makeItem     = MakeListRow;
            _assetList.bindItem     = BindListRow;
            _assetList.selectionType = SelectionType.None;

            // ── Selected panel buttons ─────────────────────────────────────
            rootVisualElement.Q<Button>("btn-add-selected").clicked += OnAddSelected;
            rootVisualElement.Q<Button>("btn-clear").clicked        += OnClearAssets;

            // ── Folder panel ───────────────────────────────────────────────
            var folderPathField = rootVisualElement.Q<TextField>("folder-path");
            folderPathField.value = _folderPath;
            folderPathField.RegisterValueChangedCallback(evt => _folderPath = evt.newValue);

            rootVisualElement.Q<Button>("btn-browse-folder").clicked += () =>
            {
                var chosen = EditorUtility.OpenFolderPanel("Source Folder", "Assets", "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    _folderPath = ToRelativePath(chosen);
                    folderPathField.SetValueWithoutNotify(_folderPath);
                }
            };

            var toggleRecursive = rootVisualElement.Q<Toggle>("toggle-recursive");
            toggleRecursive.value = _recursive;
            toggleRecursive.RegisterValueChangedCallback(evt => _recursive = evt.newValue);

            // ── Output settings ────────────────────────────────────────────
            var widthField = rootVisualElement.Q<IntegerField>("max-width");
            widthField.value = _maxWidth;
            widthField.RegisterValueChangedCallback(evt => _maxWidth = Mathf.Max(1, evt.newValue));

            var heightField = rootVisualElement.Q<IntegerField>("max-height");
            heightField.value = _maxHeight;
            heightField.RegisterValueChangedCallback(evt => _maxHeight = Mathf.Max(1, evt.newValue));

            var suffixField = rootVisualElement.Q<TextField>("name-suffix");
            suffixField.value = _namingSuffix;
            suffixField.RegisterValueChangedCallback(evt => _namingSuffix = evt.newValue);

            // ── Same-folder toggle ─────────────────────────────────────────
            var sameToggle = rootVisualElement.Q<Toggle>("toggle-same-folder");
            sameToggle.value = _sameAsSource;
            sameToggle.RegisterValueChangedCallback(evt =>
            {
                _sameAsSource = evt.newValue;
                _outputFolderRow.style.display = _sameAsSource ? DisplayStyle.None : DisplayStyle.Flex;
            });

            // ── Output folder ──────────────────────────────────────────────
            var outputFolderField = rootVisualElement.Q<TextField>("output-folder");
            outputFolderField.value = _outputFolder;
            outputFolderField.RegisterValueChangedCallback(evt => _outputFolder = evt.newValue);

            rootVisualElement.Q<Button>("btn-browse-output").clicked += () =>
            {
                var chosen = EditorUtility.OpenFolderPanel("Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    _outputFolder = ToRelativePath(chosen);
                    outputFolderField.SetValueWithoutNotify(_outputFolder);
                }
            };

            // ── Options ────────────────────────────────────────────────────
            var labelToggle = rootVisualElement.Q<Toggle>("toggle-addressables-label");
            labelToggle.value = _addAddressLabel;
            labelToggle.RegisterValueChangedCallback(evt => _addAddressLabel = evt.newValue);

            // ── Generate ───────────────────────────────────────────────────
            rootVisualElement.Q<Button>("btn-generate").clicked += OnGenerate;

            // ── Status label ───────────────────────────────────────────────
            _statusLabel = rootVisualElement.Q<Label>("status-label");

            // ── Initial visibility ─────────────────────────────────────────
            _outputFolderRow.style.display = _sameAsSource ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshPanels();
        }

        // ─── List row factory ─────────────────────────────────────────────

        private VisualElement MakeListRow()
        {
            var row = new VisualElement();
            row.AddToClassList("il-list-row");

            var icon = new Image();
            icon.AddToClassList("il-list-icon");

            var label = new Label();
            label.AddToClassList("il-list-label");

            var removeBtn = new Button();
            removeBtn.AddToClassList("il-list-remove");
            removeBtn.text = "✕";

            row.Add(icon);
            row.Add(label);
            row.Add(removeBtn);
            return row;
        }

        private void BindListRow(VisualElement element, int index)
        {
            var tex = _assets[index];

            element.Q<Image>().image = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(tex));

            var lbl = element.Q<Label>();
            lbl.text    = tex ? tex.name : "(missing)";
            lbl.tooltip = tex ? AssetDatabase.GetAssetPath(tex) : "";

            var btn = element.Q<Button>();
            // Remove old click handlers before binding.
            btn.clicked -= DummyHandler;
            btn.clicked += () => RemoveAssetAt(index);
        }

        // clicked only accepts Action; we need a stable placeholder to remove the previous lambda.
        private static readonly System.Action DummyHandler = () => { };

        private void RemoveAssetAt(int index)
        {
            if (index >= 0 && index < _assets.Count)
            {
                _assets.RemoveAt(index);
                _assetList.Rebuild();
                SetStatus($"{_assets.Count} texture(s) in list.");
            }
        }

        // ─── Button handlers ──────────────────────────────────────────────

        private void OnAddSelected()
        {
            var added = 0;
            foreach (var obj in Selection.objects)
            {
                if (obj is Texture2D tex && !_assets.Contains(tex))
                {
                    _assets.Add(tex);
                    added++;
                }
            }
            if (added > 0)
            {
                _assetList.Rebuild();
                SetStatus($"Added {added} texture(s). Total: {_assets.Count}.");
            }
            else
            {
                SetStatus("No new textures found in selection.");
            }
        }

        private void OnClearAssets()
        {
            _assets.Clear();
            _assetList.Rebuild();
            SetStatus("List cleared.");
        }

        private void OnGenerate()
        {
            if (_mode == 0)
                GenerateForSelectedAssets();
            else
                GenerateForFolder();
        }

        // ─── Generation ───────────────────────────────────────────────────

        private void GenerateForSelectedAssets()
        {
            if (_assets.Count == 0)
            {
                SetStatus("No textures in the list.");
                return;
            }

            int ok = 0, skipped = 0;
            for (var i = 0; i < _assets.Count; i++)
            {
                var tex = _assets[i];
                if (!tex) { skipped++; continue; }

                var dest = _sameAsSource
                    ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(tex)).Replace('\\', '/')
                    : _outputFolder;

                if (ProcessTexture(tex, dest))
                    ok++;
                else
                    skipped++;
            }

            Finish(ok, skipped);
        }

        private void GenerateForFolder()
        {
            if (string.IsNullOrWhiteSpace(_folderPath))
            {
                SetStatus("Please specify a source folder.");
                return;
            }

            var searchOpt = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var guids     = AssetDatabase.FindAssets("t:Texture2D", new[] { _folderPath });

            if (guids.Length == 0)
            {
                SetStatus($"No textures found in '{_folderPath}'.");
                return;
            }

            int ok = 0, skipped = 0;
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var tex       = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (!tex) { skipped++; continue; }

                // If recursive is off, skip textures in subdirectories.
                if (!_recursive && Path.GetDirectoryName(assetPath).Replace('\\', '/') != _folderPath.TrimEnd('/'))
                {
                    skipped++;
                    continue;
                }

                var dest = _sameAsSource
                    ? Path.GetDirectoryName(assetPath).Replace('\\', '/')
                    : _outputFolder;

                if (ProcessTexture(tex, dest))
                    ok++;
                else
                    skipped++;
            }

            Finish(ok, skipped);
        }

        private bool ProcessTexture(Texture2D tex, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                Debug.LogWarning($"[ImageLoader] TextureResizer: no output folder specified for '{tex.name}'. Skipping.");
                return false;
            }

            var sourcePath = AssetDatabase.GetAssetPath(tex);
            var absSource  = Path.GetFullPath(sourcePath);

            if (!File.Exists(absSource))
            {
                Debug.LogWarning($"[ImageLoader] TextureResizer: source not found at '{absSource}'. Skipping.");
                return false;
            }

            var sourceBytes = File.ReadAllBytes(absSource);
            var resizedPng  = TextureResizer.ResizeBytes(sourceBytes, _maxWidth, _maxHeight);

            if (resizedPng == null)
            {
                Debug.LogWarning($"[ImageLoader] TextureResizer: could not resize '{sourcePath}' (unsupported format or corrupt data). Skipping.");
                return false;
            }

            var outFileName = Path.GetFileNameWithoutExtension(sourcePath) + _namingSuffix + ".png";
            var outRelPath  = (outputFolder.TrimEnd('/') + "/" + outFileName);
            var outAbsPath  = Path.GetFullPath(outRelPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outAbsPath));
            File.WriteAllBytes(outAbsPath, resizedPng);
            AssetDatabase.ImportAsset(outRelPath, ImportAssetOptions.ForceUpdate);

            if (_addAddressLabel)
                AddAssetLabel(outRelPath, "ImageLoader_LowRes");

            Debug.Log($"[ImageLoader] Generated low-res: {outRelPath}");
            return true;
        }

        private void Finish(int ok, int skipped)
        {
            AssetDatabase.Refresh();
            var msg = ok > 0 ? $"Generated {ok} texture(s)." : "No textures were generated.";
            if (skipped > 0) msg += $" {skipped} skipped (see Console).";
            SetStatus(msg);
            if (ok > 0)
                EditorUtility.DisplayDialog("Done", msg, "OK");
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private void RefreshPanels()
        {
            _selectedPanel.style.display = _mode == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _folderPanel.style.display   = _mode == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetStatus(string text) => _statusLabel.text = text;

        private static void AddAssetLabel(string assetPath, string label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (asset == null) return;
            var labels = new List<string>(AssetDatabase.GetLabels(asset));
            if (!labels.Contains(label))
            {
                labels.Add(label);
                AssetDatabase.SetLabels(asset, labels.ToArray());
            }
        }

        private static string ToRelativePath(string absPath)
        {
            var dataPath = Application.dataPath;
            return absPath.StartsWith(dataPath)
                ? ("Assets" + absPath.Substring(dataPath.Length)).Replace('\\', '/')
                : absPath.Replace('\\', '/');
        }

        private static string FindEditorScriptsFolder()
        {
            var guids = AssetDatabase.FindAssets("TextureResizerEditor t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("TextureResizerEditor.cs"))
                    return Path.GetDirectoryName(path).Replace('\\', '/');
            }
            return "Assets/Editor/Scripts";
        }
    }

    // ─── Top-level menu entry ─────────────────────────────────────────────────

    /// <summary>Provides the Tools menu shortcut for the Low-Res Generator window.</summary>
    public static class TextureResizerBatchMenu
    {
        [MenuItem("Tools/ImageLoader/Low-Res Generator")]
        private static void ShowWindow()
            => EditorWindow.GetWindow<TextureResizerEditorWindow>("Low-Res Generator").Show();
    }
}

