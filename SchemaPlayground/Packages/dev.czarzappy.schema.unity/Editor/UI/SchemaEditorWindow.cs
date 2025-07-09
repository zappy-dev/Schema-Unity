using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;
using static Schema.Unity.Editor.SchemaLayout;
using Logger = Schema.Core.Logger;
using Random = System.Random;

namespace Schema.Unity.Editor
{
    [Serializable]
    public partial class SchemaEditorWindow : EditorWindow
    {
        #region Static Fields and Constants

        private const string EDITORPREFS_KEY_SELECTEDSCHEME = "Schema:SelectedSchemeName";
        
        private static ProfilerMarker _explorerViewMarker = new ProfilerMarker("SchemaEditor:ExplorerView");
        private static ProfilerMarker _tableViewMarker = new ProfilerMarker("SchemaEditor:TableView");

        private static string _defaultContentPath;
        private static string _defaultManifestLoadPath;
        
        #endregion
        
        #region Fields and Properties
        
        private bool isInitialized;
        private bool showDebugView;
        
        public SchemaResult<ManifestLoadStatus> LatestManifestLoadResponse { get; set; }
        private List<SchemaResult> responseHistory = new List<SchemaResult>();
        private DateTime latestResponseTime;
        private SchemaResult LatestResponse
        {
            get => responseHistory.LastOrDefault();
            set
            {
                latestResponseTime = DateTime.Now;
                responseHistory.Add(value);
            }
        }

        private Vector2 explorerScrollPosition;
        private Vector2 tableViewScrollPosition;

        [NonSerialized]
        private string newSchemeName = string.Empty;
        [NonSerialized]
        private string selectedSchemeName = string.Empty;
        [NonSerialized]
        private string newAttributeName = string.Empty;

        private string manifestFilePath = string.Empty;
        private string tooltipOfTheDay = string.Empty;
        
        [SerializeField]
        private int selectedSchemaIndex = -1;
        
        private FileSystemWatcher manifestWatcher;
        private DateTime lastManifestReloadTime = DateTime.MinValue;
        private readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(500);
        
        private int debugIdx;

        private readonly Dictionary<string, AttributeSortOrder> primarySchemeSort = 
            new Dictionary<string, AttributeSortOrder>();
        
        #endregion

        #region Unity Lifecycle Methods
        
        [MenuItem("Tools/Scheme Editor")]
        public static void ShowWindow()
        {
            GetWindow<SchemaEditorWindow>("Scheme Editor");
        }

        private void OnEnable()
        {
            Logger.LogDbgVerbose("Scheme Editor enabled", this);
            isInitialized = false;
            EditorApplication.update += InitializeSafely;
        }

        private void InitializeSafely()
        {
            if (isInitialized) return;
            Logger.LogDbgVerbose("InitializeSafely", this);
            
            // return;
            selectedSchemaIndex = -1;
            selectedSchemeName = string.Empty;
            newAttributeName = string.Empty;

            _defaultContentPath = Path.Combine(Path.GetFullPath(Application.dataPath + "/.."), "Content");
            _defaultManifestLoadPath = GetContentPath("Manifest.json");

            manifestFilePath = _defaultManifestLoadPath;
            // return;
            LatestResponse = OnLoadManifest("On Editor Startup");
            if (LatestResponse.Passed)
            {
                // manifest should be loaded at this point
                tooltipOfTheDay = $"Tip Of The Day: {GetTooltipMessage()}";

                InitializeFileWatcher();
            }

            var storedSelectedSchema = EditorPrefs.GetString(EDITORPREFS_KEY_SELECTEDSCHEME, null);
            // return;
            if (!string.IsNullOrEmpty(storedSelectedSchema))
            {
                Logger.LogDbgVerbose($"Selected schema found: {storedSelectedSchema}", this);
                OnSelectScheme(storedSelectedSchema, "Restoring from Editor Preferences");
            }

            EditorApplication.update -= InitializeSafely;
            isInitialized = true;
        }


        private string GetContentPath(string schemeFileName)
        {
            return Path.Combine(_defaultContentPath, schemeFileName);
        }

        #region Manifest File Changing Handling
        
        // TODO: Migrate to separate file
        private void InitializeFileWatcher()
        {
            if (manifestWatcher == null)
            {
                manifestWatcher = new FileSystemWatcher 
                {
                    Path = Path.GetDirectoryName(manifestFilePath),
                    Filter = Path.GetFileName(manifestFilePath),
                    NotifyFilter = NotifyFilters.LastWrite
                };
                manifestWatcher.Changed += OnManifestFileChanged;
                manifestWatcher.EnableRaisingEvents = true;
            }
        }

        private void OnManifestFileChanged(object sender, FileSystemEventArgs e)
        {
            DateTime now = DateTime.Now;

            if (now - lastManifestReloadTime < debounceTime) return;

            lastManifestReloadTime = now;

            // Switch to main thread using UnityEditor.EditorApplication.delayCall
            EditorApplication.delayCall += () =>
            {
                // You can now safely interact with Unity API on the main thread here
                
                OnLoadManifest($"On Manifest File Changed ");
                Repaint();
            };
        }
        #endregion

        private void OnDisable()
        {
            // Clean up event handlers to prevent memory leaks
            EditorApplication.delayCall -= InitializeSafely;
            manifestWatcher?.Dispose();
        }

        #endregion
        
        #region UI Command Handling

        private void AddSchema(DataScheme newSchema, string importFilePath = null)
        {
            bool overwriteExisting = false;
            if (DoesSchemeExist(newSchema.SchemeName))
            {
                overwriteExisting = EditorUtility.DisplayDialog("Add Schema", $"A Schema named {newSchema.SchemeName} already exists. " +
                                                                              $"Do you want to overwrite this sceham>", "Yes", "No");
            }

            newSchema.IsDirty = true;
            LatestResponse = LoadDataScheme(newSchema, overwriteExisting: overwriteExisting, importFilePath: importFilePath);
            Core.Schema.Save(true); // Adding a new schema updates the manifest
            switch (LatestResponse.Status)
            {
                case RequestStatus.Failed:
                    Debug.LogError(LatestResponse.Message);
                    break;
                case RequestStatus.Passed:
                    Debug.Log($"New scheme '{newSchema.SchemeName}' created.");
                    OnSelectScheme(newSchema.SchemeName, "Added scheme");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnSelectScheme(string schemeName, string context)
        {
            // Unfocus any selected control fields when selecting a new scheme
            GUI.FocusControl(null);
            var schemeNames = AllSchemes.ToArray();
            var prevSelectedIndex = Array.IndexOf(schemeNames, schemeName);
            if (prevSelectedIndex == -1)
            {
                return;
            }
            
            Logger.Log($"Opening Schema '{schemeName}' for editing, {context}...");
            selectedSchemeName = schemeName;
            selectedSchemaIndex = prevSelectedIndex;
            EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, schemeName);
            newAttributeName = string.Empty;
        }

        private SchemaResult OnLoadManifest(string context)
        {
            Logger.LogDbgVerbose($"Loading Manifest", context);
            // TODO: Figure out why progress reporting is making the Unity Editor unhappy
            // using var progressReporter = new EditorProgressReporter("Schema", $"Loading Manifest - {context}");
            LatestManifestLoadResponse = LoadManifestFromPath(manifestFilePath);
            LatestResponse = CheckIf(LatestManifestLoadResponse.Passed, LatestManifestLoadResponse.Message);
            return LatestResponse;
        }
        
        private void SetColumnSort(DataScheme scheme, AttributeDefinition attribute, SortOrder sortOrder)
        {
            Logger.Log($"Set column sort '{sortOrder}' for schema '{scheme.SchemeName}'.", this);
            primarySchemeSort[scheme.SchemeName] = new AttributeSortOrder(attribute.AttributeName, sortOrder);
        }

        private AttributeSortOrder GetSortOrderForScheme(DataScheme scheme)
        {
            if (primarySchemeSort.TryGetValue(scheme.SchemeName, out var sortOrder))
            {
                return sortOrder;
            }

            return AttributeSortOrder.None;
        }

        private void FocusOnEntry(string referenceSchemeName, string referenceAttributeName, string currentValue)
        {
            OnSelectScheme(referenceSchemeName, "Focus On Entry");
        }

        #endregion

        #region Rendering Methods

        private string GetTooltipMessage()
        {
            if (!GetScheme("Tooltips").Try(out var tooltips)) 
                return "Could not find Tooltips scheme";
            
            int entriesCount = tooltips.EntryCount;
            if (entriesCount == 0)
            {
                return "No tooltips found.";
            }

            Random random = new Random();
            var randomIdx = random.Next(entriesCount);
            if (!tooltips.GetEntry(randomIdx).TryGetDataAsString("Message", out var message))
                return $"No message found for tooltip entry {randomIdx}";
            
            return message;
        }
        
        private void OnGUI()
        {
            if (!isInitialized)
            {
                GUILayout.Label("Initializing...");
                return;
            }
            
            showDebugView = EditorGUILayout.Foldout(showDebugView, "Debug View");
            if (showDebugView)
            {
                RenderDebugView();
            }
            
            InitializeStyles();
            debugIdx = 0;
            GUILayout.Label("Scheme Editor", EditorStyles.largeLabel, 
                GUILayout.ExpandWidth(true));

            if (!LatestManifestLoadResponse.Passed)
            {
                EditorGUILayout.HelpBox("Hello! Would you like to Create an Empty Project or Load an Existing Project Manifest", MessageType.Info);
                if (GUILayout.Button("Start Empty Project"))
                {
                    // TODO: Handle this better? move to Schema Core?
                    LatestResponse = SaveManifest(_defaultManifestLoadPath);
                    LatestManifestLoadResponse = SchemaResult<ManifestLoadStatus>.CheckIf(LatestResponse.Passed, 
                        ManifestLoadStatus.FULLY_LOADED, 
                        LatestResponse.Message,
                        "Loaded template manifest");
                }
                return;
            }

            EditorGUILayout.HelpBox(tooltipOfTheDay, MessageType.Info);

            if (!LatestResponse.Passed && LatestResponse.Message != null)
            {
                EditorGUILayout.HelpBox(LatestResponse.Message, LatestResponse.MessageType());
            }

            GUILayout.BeginHorizontal();
            // Scrollable area to list existing schemes

            using (var _ = _explorerViewMarker.Auto())
            {
                RenderSchemaExplorer();
            }

            DrawVerticalLine();
            using (var _ = _tableViewMarker.Auto())
            {
                RenderTableView();
            }
            // Table View
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Renders the Schema sidebar to view and interact with various Schema datatypes
        /// </summary>
        private void RenderSchemaExplorer()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(400)))
            {
                GUILayout.Label($"Schema Explorer ({AllSchemes.Count()} count):", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                
                // New Schema creation form
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
                {
                    // Input field to add a new scheme
                    newSchemeName = EditorGUILayout.TextField( newSchemeName, GUILayout.ExpandWidth(false));

                    using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newSchemeName)))
                    {
                        if (AddButton("Create New Schema"))
                        {
                            var newSchema = new DataScheme(newSchemeName);
                            AddSchema(newSchema, importFilePath: GetContentPath($"{newSchemeName}.{Storage.DefaultSchemaStorageFormat.Extension}"));
                            newSchemeName = string.Empty; // clear out new scheme field name since it's unlikely someone wants to make a new scheme with the same name
                        }
                    }
                }
                
                EditorGUILayout.Space(10, false);
                    
                // render import options
                if (EditorGUILayout.DropdownButton(new GUIContent("Import"), 
                        FocusType.Keyboard, GUILayout.ExpandWidth(false)))
                {
                    GenericMenu menu = new GenericMenu();

                    foreach (var storageFormat in Storage.AllFormats)
                    {
                        menu.AddItem(new GUIContent(storageFormat.Extension.ToUpper()), false, () =>
                        {
                            if (storageFormat.TryImport(out var importedSchema, out var importFilePath))
                            {
                                AddSchema(importedSchema, importFilePath: importFilePath);
                            }
                        });
                    }
                            
                    menu.ShowAsContext();
                }
                    
                EditorGUILayout.Space(10, false);
                
                using (var explorerScrollView = new EditorGUILayout.ScrollViewScope(explorerScrollPosition))
                {
                    explorerScrollPosition = explorerScrollView.scrollPosition;
                    // list available schemes
                    string DisplayName(DataScheme s)
                    {
                        var dirtyPostfix = s.IsDirty ? "*" : string.Empty;
                        return $"{s.SchemeName}{dirtyPostfix}";
                    }
                    
                    var schemeNames = GetSchemes()
                        .Select(s => (DisplayName: DisplayName(s), SchemeName: s.SchemeName)).ToArray();

                    using (var schemeChange = new EditorGUI.ChangeCheckScope())
                    {
                        selectedSchemaIndex = GUILayout.SelectionGrid(selectedSchemaIndex, schemeNames.Select(s => s.DisplayName).ToArray(), 1, LeftAlignedButtonStyle);
                        
                        if (schemeChange.changed)
                        {
                            var nextSelectedSchema = schemeNames[selectedSchemaIndex];
                            OnSelectScheme(nextSelectedSchema.SchemeName, "Selected Schema in Explorer");
                        }
                    }
                }
            }
        }

        public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            r.height = thickness;
            r.y+=padding/2.0f;
            r.x-=2;
            r.width +=6;
            EditorGUI.DrawRect(r, color);
        }

        #region Utilities

        void Mark()
        {
            EditorGUILayout.LabelField($"Mark{debugIdx++}", GUILayout.ExpandWidth(false), GUILayout.Width(50));
        }
        
        private void DrawVerticalLine(float thickness = 2)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(thickness), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, Color.white);
        }

        public static bool AddButton(string text, bool expandWidth = false, float? height = null) => 
            Button(text, EditorIcon.PLUS_ICON_NAME, expandWidth: expandWidth, height: height);

        public static bool Button(string text, string iconName, bool expandWidth = false, float? height = null) =>
            Button(text, iconName, GUILayout.ExpandWidth(expandWidth),
                height != null ? GUILayout.Height(height.Value) : GUILayout.ExpandHeight(false));
        
        
        public static bool Button(string text, string iconName, params GUILayoutOption[] options) =>
            GUILayout.Button(new GUIContent(text, EditorGUIUtility.IconContent(iconName).image), options);
        
        #endregion
        
        #endregion
    }
}