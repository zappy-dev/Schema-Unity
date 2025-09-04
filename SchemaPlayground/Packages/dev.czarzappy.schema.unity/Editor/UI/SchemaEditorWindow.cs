using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Random = System.Random;
using Schema.Core.Schemes;
using Schema.Unity.Editor.Ext;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;
using static Schema.Unity.Editor.SchemaLayout;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    [Serializable]
    internal partial class SchemaEditorWindow : EditorWindow
    {
        #region Static Fields and Constants

        private static readonly SchemaContext Context = new SchemaContext
        {
            Driver = "Editor",
        };

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

        [NonSerialized]
        private string newSchemeName = string.Empty;
        [NonSerialized]
        private string selectedSchemeName = string.Empty;

        private string SelectedSchemeName
        {
            get => selectedSchemeName;
            set
            {
                if (selectedSchemeName == value)
                {
                    return;
                }

                selectedSchemeName = value;
                
                OnSelectedSchemeChanged?.Invoke();
            }
        }

        private event Action OnSelectedSchemeChanged;
        
        [NonSerialized]
        private string newAttributeName = string.Empty;
        private string tooltipOfTheDay = string.Empty;
        
        [SerializeField]
        private int selectedSchemaIndex = -1;
        
        private FileSystemWatcher manifestWatcher;
        private DateTime lastManifestReloadTime = DateTime.MinValue;
        private readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(500);
        
        private int debugIdx;

        private readonly Dictionary<string, AttributeSortOrder> primarySchemeSort = new Dictionary<string, AttributeSortOrder>();

        // Undo/Redo UI toggle
        private bool _showUndoRedoPanel = true;
        
        // Virtual scrolling support
        private VirtualTableView _virtualTableView;
        private Rect lastScrollViewRect = Rect.zero;

        #endregion

        #region Unity Lifecycle Methods
        
        [MenuItem("Tools/Scheme Editor")]
        public static void ShowWindow()
        {
            GetWindow<SchemaEditorWindow>("Scheme Editor");
        }

        private void OnEnable()
        {
            LogDbgVerbose("Scheme Editor enabled", this);
            isInitialized = false;
            EditorApplication.update += InitializeSafely;
            RegisterCommandHistoryCallbacks();
            
            // Initialize virtual scrolling
            _virtualTableView = new VirtualTableView();

            OnAttributeFiltersUpdated += RefreshTableEntriesForSelectedScheme;
            OnSelectedSchemeChanged += RefreshTableEntriesForSelectedScheme;
            OnSelectedSchemeChanged += () =>
            {
                if (!string.IsNullOrEmpty(selectedSchemeName))
                {
                    LoadAttributeFilters(selectedSchemeName);
                }

                selectedSchemeLoadPath = null;
            };
        }

        private void OnDisable()
        {
            // Clean up event handlers to prevent memory leaks
            EditorApplication.delayCall -= InitializeSafely;
            manifestWatcher?.Dispose();
            
            UnregisterCommandHistoryCallbacks();
        }

        private void InitializeSafely()
        {
            if (isInitialized) return;
            EditorApplication.update -= InitializeSafely;
            LogDbgVerbose("InitializeSafely", this);
            
            // return;
            selectedSchemaIndex = -1;
            SelectedSchemeName = string.Empty;
            newAttributeName = string.Empty;

            _defaultContentPath = Path.Combine(Path.GetFullPath(Application.dataPath + "/.."), "Content");
            _defaultManifestLoadPath = GetContentPath("Manifest.json");

            // if (!IsInitialized)
            // {
                ManifestImportPath = _defaultManifestLoadPath;
            // }
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
                LogDbgVerbose($"Selected schema found: {storedSelectedSchema}", this);
                OnSelectScheme(storedSelectedSchema, "Restoring from Editor Preferences");
            }

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
                    Path = Path.GetDirectoryName(ManifestImportPath),
                    Filter = Path.GetFileName(ManifestImportPath),
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

        #endregion
        
        #region UI Command Handling

        private void OnSelectScheme(string schemeName, string context)
        {
            // Unfocus any selected control fields when selecting a new scheme
            ReleaseControlFocus();
            var schemeNames = AllSchemes.ToArray();
            var prevSelectedIndex = Array.IndexOf(schemeNames, schemeName);
            if (prevSelectedIndex == -1)
            {
                return;
            }
            
            Log($"Opening Schema '{schemeName}' for editing, {context}...");
            SelectedSchemeName = schemeName;
            selectedSchemaIndex = prevSelectedIndex;
            EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, schemeName);
            newAttributeName = string.Empty;
            
            // Clear virtual scrolling cache when switching schemes
            _virtualTableView?.ClearCache();
        }

        private SchemaResult OnLoadManifest(string context)
        {
            LogDbgVerbose($"Loading Manifest", context);
            // TODO: Figure out why progress reporting is making the Unity Editor unhappy
            // using var progressReporter = new EditorProgressReporter("Schema", $"Loading Manifest - {context}");
            LatestManifestLoadResponse = LoadManifestFromPath(ManifestImportPath, Context);
            LatestResponse = CheckIf(LatestManifestLoadResponse.Passed, LatestManifestLoadResponse.Message, LatestManifestLoadResponse.Context);
            return LatestResponse;
        }
        
        private void SetColumnSort(DataScheme scheme, AttributeDefinition attribute, SortOrder sortOrder)
        {
            Log($"Set column sort '{sortOrder}' for schema '{scheme.SchemeName}'.", this);
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

        /// <summary>
        /// Utility method for release focus from a selected control.
        /// Selecting a control can prevent it from updating with new values. By forcing a release of the focus, these controls can repaint with new values
        /// </summary>
        private void ReleaseControlFocus()
        {
            GUI.FocusControl(null);
        }
        private string GetTooltipMessage()
        {
            if (!GetScheme("Tooltips").Try(out var tooltipDataScheme)) 
                return "Could not find Tooltips scheme";

            var tooltips = new TooltipsScheme(tooltipDataScheme);
            
            int entriesCount = tooltips.EntryCount;
            if (entriesCount == 0)
            {
                return "No tooltips found.";
            }

            Random random = new Random();
            var randomIdx = random.Next(entriesCount);
            return tooltips.GetEntry(randomIdx).Message;
        }
        
        
        private void OnGUI()
        {
            if (!isInitialized)
            {
                GUILayout.Label("Initializing...");
                return;
            }
            
            // showDebugView = EditorGUILayout.Foldout(showDebugView, "Debug View");
            // if (showDebugView)
            // {
            //     RenderDebugView();
            // }
            
            InitializeStyles();
            debugIdx = 0;
            GUILayout.Label("Scheme Editor", EditorStyles.largeLabel, 
                ExpandWidthOptions);
            
            if (!LatestManifestLoadResponse.Passed)
            {
                EditorGUILayout.HelpBox("Welcome to Schema! Would you like to Create an Empty Project or Load an Existing Project Manifest", MessageType.Info);
                if (GUILayout.Button("Create Empty Project"))
                {
                    // TODO: Handle this better? move to Schema Core?
                    LatestResponse = SaveManifest();
                    LatestManifestLoadResponse = SchemaResult<ManifestLoadStatus>.CheckIf(LatestResponse.Passed, 
                        ManifestLoadStatus.FULLY_LOADED, 
                        LatestResponse.Message,
                        "Loaded template manifest", Context);
                }
            }
            
            GUILayout.Label("Manifest Path");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope())
                {
                    EditorGUILayout.TextField("Manifest Import Path", ManifestImportPath);
#if SCHEMA_DEBUG
                    EditorGUILayout.IntField("Loaded Manifest Scheme Hash",
                        RuntimeHelpers.GetHashCode(LoadedManifestScheme._));
#endif
                }

                if (GUILayout.Button("Load", DoNotExpandWidthOptions))
                {
                    OnLoadManifest("On User Load");
                }

                if (GUILayout.Button("Open", DoNotExpandWidthOptions))
                {
                    EditorUtility.RevealInFinder(ManifestImportPath);
                }

                // save schemes to manifest
                if (GUILayout.Button("Save Manifest", DoNotExpandWidthOptions))
                {
                    LatestResponse = SaveManifest();
                }
            }

#if SCHEMA_DEBUG
            if (LatestManifestLoadResponse.Message != null)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestManifestLoadResponse.Result}: {LatestManifestLoadResponse.Message}", LatestManifestLoadResponse.MessageType());
            }
            
            if (LatestResponse.Message != null)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestResponse.Message}", LatestResponse.MessageType());
            }
#endif

            // Do not render more until we have valid manifest loaded
            if (LatestManifestLoadResponse.Failed) return;
            
            // NEW: Undo/Redo controls and progress display
            DrawUndoRedoPanel();
            DrawProgressBar();

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
                GUILayout.Label($"Schema Explorer ({AllSchemes.Count()} count):", EditorStyles.boldLabel, DoNotExpandWidthOptions);
                
                // New Schema creation form
                using (new EditorGUILayout.HorizontalScope(DoNotExpandWidthOptions))
                {
                    // Input field to add a new scheme
                    newSchemeName = EditorGUILayout.TextField( newSchemeName, DoNotExpandWidthOptions);

                    using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newSchemeName)))
                    {
                        if (AddButton("Create New Schema"))
                        {
                            var newSchema = new DataScheme(newSchemeName);
                            
                            // Create a relative path for the new schema file
                            string fileName = $"{newSchemeName}.{Storage.DefaultSchemaStorageFormat.Extension}";
                            string relativePath = fileName; // Default to just the filename (relative to Content folder)
                            
                            // Get the full path for actual file creation
                            string fullPath = GetContentPath(fileName);
                            
                            SubmitAddSchemeRequest(newSchema, importFilePath: relativePath).FireAndForget();
                            newSchemeName = string.Empty; // clear out new scheme field name since it's unlikely someone wants to make a new scheme with the same name
                        }
                    }
                }
                
                EditorGUILayout.Space(10, false);
                    
                // render import options
                if (EditorGUILayout.DropdownButton(new GUIContent("Import"), 
                        FocusType.Keyboard, DoNotExpandWidthOptions))
                {
                    GenericMenu menu = new GenericMenu();

                    foreach (var storageFormat in Storage.AllFormats)
                    {
                        if (!storageFormat.IsImportSupported)
                        {
                            continue;
                        }

                        if (storageFormat is ScriptableObjectStorageFormat so)
                        {
                            foreach (var soType in TypeUtils.GetUserDefinedScriptableObjectTypes())
                            {
                                menu.AddItem(new GUIContent($"{storageFormat.DisplayName}/{soType.Name} - {soType.Assembly.GetName().Name}"), false, () =>
                                {
                                    ImportScriptableObjectToDataScheme(soType);
                                });
                            }
                        }
                        else
                        {
                            menu.AddItem(new GUIContent(storageFormat.DisplayName), false, () =>
                            {
                                if (storageFormat.TryImport(out var importedSchema, out var importFilePath))
                                {
                                    SubmitAddSchemeRequest(importedSchema, importFilePath: importFilePath).FireAndForget();
                                }
                            });
                        }
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
        
        #endregion
        
        /// <summary>
        /// Imports a given type of a Scriptable Object as a new or updated Data Scheme
        /// </summary>
        /// <param name="soType">Type of Scriptable Object to Import</param>
        private void ImportScriptableObjectToDataScheme(Type soType)
        {
            // Create a data Scheme from a ScriptableObject
            var newSOSchemeName = soType.Name;

            string operationTitle = $"Schema - Import Scheme for '{newSOSchemeName}' Scriptable Object";
            using var progress = new ProgressScope(operationTitle);
            progress.Progress($"Scanning for available Fields", 0.1f);
            var serializedFields = TypeUtils.GetSerializedFieldsForType(soType);

            progress.Progress($"Mapping fields to available Data Types", 0.3f);
            var dataScheme = new DataScheme(newSOSchemeName);
            
            // Create an attribute for mapping scheme entries to backing assets via asset guids
            var assetGUIDAttrRes = dataScheme.AddAttribute("Asset", DataType.Guid, isIdentifier: true);
            if (!assetGUIDAttrRes.Try(out var assetGuidAttr))
            {
                LogError(assetGUIDAttrRes.Message, assetGUIDAttrRes.Context);
                return;
            }
            
            var mappedFields = new List<FieldInfo>();
            foreach (var field in serializedFields)
            {
                DataType dataType = null;
                if (field.FieldType == typeof(int))
                {
                    dataType = DataType.Integer;
                }
                else if (field.FieldType == typeof(float))
                {
                    dataType = DataType.Float;
                }
                else if (field.FieldType == typeof(bool))
                {
                    dataType = DataType.Boolean;
                } 
                else if (field.FieldType == typeof(string))
                {
                    dataType = DataType.Text;
                }
                else if (field.FieldType == typeof(Guid))
                {
                    dataType = DataType.Guid;
                }
                else if (field.FieldType.IsEnum)
                {
                    var enumSchemeName = field.FieldType.Name;
                    AttributeDefinition enumIdAttr = null;
                    // Enumeration
                    // First try to find if the enumeration already exists as another Scheme
                    // If it doesn't already, then prompt the user to create one
                    if (!GetScheme(enumSchemeName).Try(out var enumScheme))
                    {
                        if (EditorUtility.DisplayDialog(operationTitle,
                                $"No existing data scheme for enum '{enumSchemeName}'. Do you want to create one?", "Yes, create new Scheme", "Skip"))
                        {
                            // Create a new data scheme for referenced enum
                            enumScheme = new DataScheme(enumSchemeName);
                            // Create ID attribute to reference
                            var enumIdAttrRes = enumScheme.AddAttribute("ID", DataType.Text, isIdentifier: true);
                            if (!enumIdAttrRes.Try(out enumIdAttr))
                            {
                                LogError(enumIdAttrRes.Message, enumIdAttrRes.Context);
                                continue;
                            }
                                                    
                            // time to add data entries for enum
                            var enumValues = Enum.GetValues(field.FieldType);
                            foreach (var enumValue in enumValues)
                            {
                                var enumDataEntry = new DataEntry();
                                enumDataEntry.SetData(enumIdAttr.AttributeName, enumValue.ToString());
                                var addEntryRes = enumScheme.AddEntry(enumDataEntry);
                                if (addEntryRes.Failed)
                                {
                                    LogError(addEntryRes.Message, addEntryRes.Context);
                                    continue;
                                }
                            }
                                                    
                            // finally save new data scheme
                            string enumSchemeFileName = $"{enumSchemeName}.{Storage.DefaultSchemaStorageFormat.Extension}";
                            SubmitAddSchemeRequest(enumScheme, importFilePath: enumSchemeFileName).FireAndForget();
                        }
                    }
                    else
                    {
                        var enumIdAttrRes = enumScheme.GetIdentifierAttribute();
                        if (!enumIdAttrRes.Try(out enumIdAttr))
                        {
                            LogError(enumIdAttrRes.Message, enumIdAttrRes.Context);
                            continue;
                        }
                    }
                                            
                    // Need to finally reference this enum ID attribute
                    dataType = new ReferenceDataType(enumSchemeName, enumIdAttr.AttributeName);
                }
                else if (field.FieldType == soType)
                {
                    // Handle self references
                    // Create a Reference Data Type that references self scheme
                    dataType = new ReferenceDataType(newSOSchemeName, assetGuidAttr.AttributeName);
                }

                // TODO: How to handle field type references for non-scriptable objects?
                // Option 1. Create a new schema for those?
                // Option 2. Support complex-nested objects..
                if (dataType == null)
                {
                    LogWarning(
                        $"No DataType mapping for C# type: {field.FieldType}, field: {field}");
                }
                else
                {
                    mappedFields.Add(field);
                    dataScheme.AddAttribute(field.Name, dataType);
                }
            }
                                    
            progress.Progress($"Searching for existing ScriptableObject assets", 0.5f);
            var soAssets = AssetDatabase.FindAssets($"t:{newSOSchemeName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadMainAssetAtPath);
                                    
            progress.Progress($"Creating data entries for existing assets", 0.7f);
            // First pass, setup initial entries

            var entries = soAssets.Select((asset) =>
            {
                var dataEntry = new DataEntry();

                var assetPath = AssetDatabase.GetAssetPath(asset);
                var assetGuid = Guid.Parse(AssetDatabase.AssetPathToGUID(assetPath));
                var setAssetGuidRes = dataEntry.SetData(assetGuidAttr.AttributeName, assetGuid);

                return (asset, dataEntry, setAssetGuidRes);
            }).Where((result) =>
            {
                var (_, _, setAssetGuidRes) = result;
                if (setAssetGuidRes.Failed)
                {
                    LogError(setAssetGuidRes.Message, setAssetGuidRes.Context);
                    return false;
                }

                return true;
            });
            
            foreach (var (asset, dataEntry, _) in entries)
            {
                // First pass
                bool runValidation = true;
                foreach (var field in mappedFields)
                {
                    // Map an enum value to a string
                    var rawValue = field.GetValue(asset);

                    object entryValue = rawValue;
                    if (field.FieldType.IsEnum)
                    {
                        // Reference string value for enum
                        entryValue = rawValue?.ToString();
                    }
                    else if (field.FieldType == soType)
                    {
                        runValidation = false;
                        // Reference asset guid of target object
                        var otherAsset = entries.FirstOrDefault((entry) =>
                        {
                            var (otherObject, otherDataEntry, _) = entry;
                            return otherObject == entryValue;
                        });

                        if (otherAsset.dataEntry != default)
                        {
                            // TODO: fix this... the reference type itself is a guid...
                            // should this value match the reference's attribute (i.e a guid?)
                            entryValue = otherAsset.dataEntry.GetDataDirect(assetGuidAttr);
                            LogVerbose($"Setting reference: {entryValue}");
                        }
                    }
                    var setDataRes = dataEntry.SetData(field.Name, entryValue);
                    if (setDataRes.Failed)
                    {
                        LogError(setDataRes.Message, setDataRes.Context);
                    }
                }

                var res = dataScheme.AddEntry(dataEntry, runValidation);
                if (res.Failed)
                {
                    Logger.LogError(res.Message, res.Context);
                    continue;
                }
            }

            progress.Progress($"Loading final Data Scheme", 0.9f);
            string fileName = $"{newSOSchemeName}.{Storage.DefaultSchemaStorageFormat.Extension}";
            SubmitAddSchemeRequest(dataScheme, importFilePath: fileName).FireAndForget();
        }

        private SchemaResult PublishScheme(string schemeName)
        {
            LogDbgVerbose($"Publishing {schemeName}");
            var schemeEntry = GetManifestEntryForScheme(schemeName);
            if (!schemeEntry.Try(out ManifestEntry manifestEntry) ||
                !GetScheme(schemeName).Try(out var schemeToPublish))
            {
                return Fail($"Could not find manifest entry for scheme to publish, scheme: {schemeName}");
            }

            // TODO: Figure out how to better enumerate this, enforce Manifest scheme attribute is valid
            // Problem: Manifest Scheme loads first, before any other scheme, so if it has a reference data type attribute to another scheme, that is invalid
            // could skip validation and validate after load?
            // But seems hacky
            switch (manifestEntry.PublishTarget)
            {
                case "SCRIPTABLE_OBJECT":

                    // HACK: Assumes ID column is the asset guid for an underlying scriptable object to publish to
                    if (!schemeToPublish.GetIdentifierAttribute().Try(out var idAttr))
                    {
                        return Fail("No identifier attribute found for scheme to publish", schemeToPublish.Context);
                    }
                    
                    foreach (var entry in schemeToPublish.AllEntries)
                    {
                        PublishScriptableObjectEntry(schemeToPublish, entry, idAttr);
                    }
                    break;
                default:
                    break;
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return Pass("Published assets");
        }

        private SchemaResult PublishScriptableObjectEntry(DataScheme schemeToPublish, DataEntry entry, AttributeDefinition idAttr)
        {
            var assetGuid = entry.GetDataAsGuid(idAttr.AttributeName);

            if (!AssetUtils.TryLoadAssetFromGUID(assetGuid, out var currentAsset))
            {
                return Fail($"Not asset found with guid: {assetGuid}");
            }
                        
            var assetType = currentAsset.GetType();

            var soFields = TypeUtils.GetSerializedFieldsForType(assetType);
            var fieldMap = schemeToPublish.GetAttributes()
                .ToDictionary(attr => attr.AttributeName, attr => soFields.FirstOrDefault(field => field.Name == attr.AttributeName));
            
            // BUG: Referenced Data Type stops matching Reference'd Data Type
            foreach (var kvp in entry)
            {
                var attrName = kvp.Key;
                var value = kvp.Value;
                
                // HACK: Skip Asset GUID Id field
                if (attrName == idAttr.AttributeName) continue;
                            
                var field = fieldMap[attrName];
                if (!schemeToPublish.GetAttribute(attrName).Try(out var attr))
                {
                    LogError($"No attribute found for entry key: {attrName}");
                    continue;
                }
                            
                try
                {
                    object mappedValue = null;
                    if (field.FieldType.IsEnum)
                    {
                        mappedValue = Enum.Parse(field.FieldType, value?.ToString() ?? string.Empty);
                    }
                    else
                    {
                        switch (attr.DataType)
                        {
                            // HACK: I know in this case the one reference data type we have is an asset guid reference, just resolve that reference
                            case ReferenceDataType refDataType:

                                if (!schemeToPublish.TryGetEntry(searchEntry =>
                                        searchEntry.GetData(refDataType.ReferenceAttributeName) == value, out var refEntry))
                                {
                                    LogDbgError($"Failed to find referenced entry for value: {value} ({value?.GetType()})");
                                    continue;
                                }

                                var refGuid = refEntry.GetDataAsGuid(refDataType.ReferenceAttributeName);
                                if (AssetUtils.TryLoadAssetFromGUID(refGuid, out var refAsset))
                                {
                                    mappedValue = refAsset;
                                }
                                break;
                            default:
                                mappedValue = value;
                                break;
                        }
                    }
                    field.SetValue(currentAsset, mappedValue);
                    LogDbgVerbose($"{field.Name}=>{mappedValue}");
                }
                catch (Exception e)
                {
                    LogError(e.Message);
                }
            }
            
            LogDbgVerbose($"Saving changes to asset: {currentAsset}");
            EditorUtility.SetDirty(currentAsset);

            return Pass($"Saving changes to asset: {currentAsset}");
        }
    }
}