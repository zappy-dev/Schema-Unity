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
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;
using static Schema.Unity.Editor.SchemaLayout;
using Random = System.Random;
using Schema.Core.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace Schema.Unity.Editor
{
    [Serializable]
    public class SchemaEditorWindow : EditorWindow
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
        
        // NEW FIELDS FOR ASYNC COMMAND SYSTEM
        private readonly ICommandHistory _commandHistory = new CommandHistory();
        private CancellationTokenSource _cancellationTokenSource;

        // Progress tracking
        private bool _operationInProgress;
        private string _currentOperationDescription;
        private float _currentProgress;
        private string _currentProgressMessage;

        // Undo/Redo UI toggle
        private bool _showUndoRedoPanel = true;
        
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
            _cancellationTokenSource = new CancellationTokenSource();
            // Subscribe to command history events for repainting
            _commandHistory.CommandExecuted += (_, __) => Repaint();
            _commandHistory.CommandUndone += (_, __) => Repaint();
            _commandHistory.CommandRedone += (_, __) => Repaint();
        }

        private void InitializeSafely()
        {
            if (isInitialized) return;
            LogDbgVerbose("InitializeSafely", this);
            
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
                LogDbgVerbose($"Selected schema found: {storedSelectedSchema}", this);
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
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _commandHistory.CommandExecuted -= (_, __) => Repaint();
            _commandHistory.CommandUndone -= (_, __) => Repaint();
            _commandHistory.CommandRedone -= (_, __) => Repaint();
        }

        #endregion
        
        #region UI Command Handling

        // Async version of adding a schema using command system
        private async Task AddSchemaAsync(DataScheme newSchema, string importFilePath = null)
        {
            if (_operationInProgress) return;

            // Confirm overwrite if needed
            bool overwriteExisting = false;
            if (DoesSchemeExist(newSchema.SchemeName))
            {
                overwriteExisting = EditorUtility.DisplayDialog(
                    "Add Schema",
                    $"A Schema named {newSchema.SchemeName} already exists. Do you want to overwrite this scheme?",
                    "Yes",
                    "No");
                if (!overwriteExisting) return; // user cancelled
            }

            _operationInProgress = true;
            _currentOperationDescription = $"Adding schema '{newSchema.SchemeName}'";

            try
            {
                var progress = new Progress<CommandProgress>(UpdateProgress);
                var command = new LoadDataSchemeCommand(
                    newSchema,
                    overwriteExisting: overwriteExisting,
                    progress: progress);

                var result = await _commandHistory.ExecuteAsync(command, _cancellationTokenSource.Token);

                if (result.IsSuccess)
                {
                    OnSelectScheme(newSchema.SchemeName, "Added schema");
                    // Persist changes
                    Core.Schema.Save(true);
                }
                else
                {
                    Debug.LogError(result.Message);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Add schema operation cancelled");
            }
            finally
            {
                _operationInProgress = false;
                _currentProgress = 0f;
                _currentProgressMessage = string.Empty;
                Repaint();
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
            
            Log($"Opening Schema '{schemeName}' for editing, {context}...");
            selectedSchemeName = schemeName;
            selectedSchemaIndex = prevSelectedIndex;
            EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, schemeName);
            newAttributeName = string.Empty;
        }

        private SchemaResult OnLoadManifest(string context)
        {
            LogDbgVerbose($"Loading Manifest", context);
            // TODO: Figure out why progress reporting is making the Unity Editor unhappy
            // using var progressReporter = new EditorProgressReporter("Schema", $"Loading Manifest - {context}");
            LatestManifestLoadResponse = LoadManifestFromPath(manifestFilePath);
            LatestResponse = CheckIf(LatestManifestLoadResponse.Passed, LatestManifestLoadResponse.Message);
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

            // NEW: Undo/Redo controls and progress display
            DrawUndoRedoPanel();
            DrawProgressBar();

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

        private void RenderDebugView()
        {
            using (new EditorGUI.DisabledScope())
            {
                EditorGUILayout.Toggle("Is Schema Initialized?", IsInitialized);
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Manifest Path");
                
                using (new EditorGUI.DisabledScope())
                {
                    GUILayout.TextField(manifestFilePath);
                }

                if (GUILayout.Button("Load"))
                {
                    OnLoadManifest("On User Load");
                }

                // save schemes to manifest
                if (GUILayout.Button("Save"))
                {
                    Debug.Log($"Saving manifest to {manifestFilePath}");
                    LatestResponse = SaveManifest(manifestFilePath);
                }
            }

            if (GUILayout.Button("Fix Duplicate Entries"))
            {
                foreach (var schemeName in AllSchemes)
                {
                    if (!GetScheme(schemeName).Try(out var scheme))
                    {
                        continue; // can't de-dupe these schemes easily
                    }

                    if (!scheme.GetIdentifierAttribute().Try(out var identifierAttribute))
                    {
                        identifierAttribute = scheme.GetAttribute(0); // just use first attribute
                    }

                    var identifiers = scheme.GetValuesForAttribute(identifierAttribute).ToArray();
                    bool[] foundEntry = new bool[identifiers.Length];
                    int numDeleted = 0;
                    for (int i = 0; i < scheme.EntryCount; i++)
                    {
                        var entry = scheme.GetEntry(i);
                        var entryIdValue = entry.GetDataAsString(identifierAttribute.AttributeName);
                        int identifierIdx = Array.IndexOf(identifiers, entryIdValue);

                        if (identifierIdx != -1 && foundEntry[identifierIdx])
                        {
                            scheme.DeleteEntry(entry);
                            numDeleted++;
                        }
                        else
                        {
                            foundEntry[identifierIdx] = true;
                        }
                    }

                    if (numDeleted > 0)
                    {
                        LogWarning($"Scheme '{schemeName}' has deleted {numDeleted} entries.");
                        SaveDataScheme(scheme, false);
                    }
                }
            }

            if (LatestManifestLoadResponse.Message != null)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestManifestLoadResponse.Result}: {LatestManifestLoadResponse.Message}", LatestManifestLoadResponse.MessageType());
            }
            
            if (LatestResponse.Message != null)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestResponse.Message}", LatestResponse.MessageType());
            }

            if (GUILayout.Button("Add SCHEMA_DEBUG Scripting Define"))
            {
                var buildTargetGroup = UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup;
                var defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                if (!defines.Contains("SCHEMA_DEBUG"))
                {
                    if (!string.IsNullOrEmpty(defines))
                        defines += ";";
                    defines += "SCHEMA_DEBUG";
                    UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
                    Debug.Log("Added SCHEMA_DEBUG scripting define.");
                }
            }
            if (GUILayout.Button("Remove SCHEMA_DEBUG Scripting Define"))
            {
                var buildTargetGroup = UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup;
                var defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                if (defines.Contains("SCHEMA_DEBUG"))
                {
                    var newDefines = string.Join(";", defines.Split(';').Where(d => d != "SCHEMA_DEBUG"));
                    UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
                    Debug.Log("Removed SCHEMA_DEBUG scripting define.");
                }
            }
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
                            AddSchemaAsync(newSchema, importFilePath: GetContentPath($"{newSchemeName}.{Storage.DefaultSchemaStorageFormat.Extension}"));
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
                                AddSchemaAsync(importedSchema, importFilePath: importFilePath);
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

        private void RenderTableView()
        {
            if (string.IsNullOrEmpty(selectedSchemeName))
            {
                EditorGUILayout.HelpBox("Choose a Schema from the Schema Explorer to view in the table", MessageType.Info);
                return;
            }

            if (!GetScheme(selectedSchemeName).Try(out var scheme))
            {
                EditorGUILayout.HelpBox($"Schema '{selectedSchemeName}' does not exist.", MessageType.Warning);
                return;
            }
            
            int attributeCount = scheme.AttributeCount;
            int entryCount = scheme.EntryCount;
                
            // mapping entries to control IDs for focus/navigation management
            int[] tableCellControlIds = new int[attributeCount * entryCount];

            using (new GUILayout.VerticalScope())
            {
                GUILayout.Label($"Table View - {scheme.SchemeName} - {entryCount} {(entryCount == 1 ? "entry" : "entries")}", EditorStyles.boldLabel);

                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Storage Path", GUILayout.ExpandWidth(false));
                    using (new EditorGUI.DisabledScope())
                    {
                        string storagePath = "No storage path found for this Schema.";
                        if (GetManifestEntryForScheme(scheme).Try(out var schemeManifest) && 
                            schemeManifest.TryGetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH, out storagePath))
                        {
                        }
                        
                        EditorGUILayout.TextField(storagePath);
                    }
                    
                    var dirtyPostfix = scheme.IsDirty ? "*" : string.Empty;
                    
                    if (GUILayout.Button($"Save{dirtyPostfix}", GUILayout.ExpandWidth(true)))
                    {
                        LatestResponse = SaveDataScheme(scheme, alsoSaveManifest: false);
                    }
                    
                    // TODO: Open raw schema files
                    // if (GUILayout.Button("Open", GUILayout.ExpandWidth(true)) &&
                    //     GetManifestEntryForScheme(scheme).Try(out var manifestEntry))
                    // {
                    //     var storagePath = manifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
                    //     Application.OpenURL(storagePath);
                    // }
                    
                    // render export options
                    if (EditorGUILayout.DropdownButton(new GUIContent("Export"), 
                            FocusType.Keyboard, GUILayout.ExpandWidth(false)))
                    {
                        GenericMenu menu = new GenericMenu();

                        foreach (var storageFormat in Storage.AllFormats)
                        {
                            menu.AddItem(new GUIContent(storageFormat.Extension.ToUpper()), false, () =>
                            {
                                storageFormat.Export(scheme);
                            });
                        }
                        
                        menu.ShowAsContext();
                    }
                }
                
                
                tableViewScrollPosition = GUILayout.BeginScrollView(tableViewScrollPosition, alwaysShowHorizontal: true, alwaysShowVertical: true);
                
                // TODO: Figure out how to freeze the table header so it doesn't scroll
                RenderTableHeader(attributeCount, scheme);
                
                var sortOrder = GetSortOrderForScheme(scheme);
                var entries = scheme.GetEntries(sortOrder);
                
                // render table body, scheme data entries
                int entryIdx = 0;
                foreach (var entry in entries)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        var cellStyle = GetRowCellStyle(entryIdx);
                        
                        // render entry values
                        using (new BackgroundColorScope(cellStyle.BackgroundColor))
                        {
                            // row settings gear
                            // make row count human 1-based
                            if (DropdownButton($"{entryIdx + 1}", style: cellStyle.DropdownStyle))
                            {
                                RenderEntryRowOptions(entryIdx, entryCount, scheme, entry);
                            }
                            
                            for (int attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                            {
                                var attribute = scheme.GetAttribute(attributeIdx);
                                var attributeName = attribute.AttributeName;
                                // handle setting
                                object entryValue;
                                bool didSetLoad = false;
                                if (!entry.GetData(attribute).Try(out entryValue))
                                {
                                    LogDbgWarning($"Setting {attribute} data for {entry}");
                                    // for some reason this data wasn't set yet
                                    entryValue = attribute.CloneDefaultValue();
                                    didSetLoad = true;
                                }
                                else
                                {
                                    if (attribute.DataType.CheckIfValidData(entryValue).Failed)
                                    {
                                        if (DataType.ConvertData(entryValue, DataType.Default, attribute.DataType)
                                            .Try(out var convertedValue))
                                        {
                                            entryValue = convertedValue;
                                            didSetLoad = true;
                                        }
                                        else
                                        {
                                            entryValue = attribute.CloneDefaultValue();
                                            didSetLoad = true;
                                        }
                                    }
                                }

                                if (didSetLoad)
                                {
                                    _ = ExecuteSetDataOnEntryAsync(scheme, entry, attributeName, entryValue);
                                }

                                var attributeFieldWidth = GUILayout.Width(attribute.ColumnWidth);
                                using (var changed = new EditorGUI.ChangeCheckScope())
                                {
                                    if (attribute.DataType == DataType.Integer)
                                    {
                                        entryValue = EditorGUILayout.IntField(Convert.ToInt32(entryValue), cellStyle.FieldStyle,
                                            attributeFieldWidth);
                                    }
                                    else if (attribute.DataType is BooleanDataType)
                                    {
                                        bool boolValue = false;
                                        if (entryValue is bool b)
                                            boolValue = b;
                                        else if (entryValue is string s && bool.TryParse(s, out var parsed))
                                            boolValue = parsed;
                                        else if (entryValue is int i)
                                            boolValue = i != 0;
                                        entryValue = EditorGUILayout.Toggle(boolValue, attributeFieldWidth);
                                    }
                                    else if (attribute.DataType == DataType.FilePath)
                                    {
                                        string filePath = entryValue.ToString();
                                        var filePreview = Path.GetFileName(filePath);
                                        if (string.IsNullOrEmpty(filePreview))
                                        {
                                            filePreview = "...";
                                        }
                                        var fileContent = new GUIContent(filePreview, tooltip: filePath);
                                        if (GUILayout.Button(fileContent, cellStyle.ButtonStyle, attributeFieldWidth))
                                        {
                                            var selectedFilePath = EditorUtility.OpenFilePanel($"{scheme.SchemeName} - {attributeName}", "", "");
                                            if (!string.IsNullOrEmpty(selectedFilePath))
                                            {
                                                entryValue = selectedFilePath;
                                            }
                                        }
                                    }
                                    else if (attribute.DataType is ReferenceDataType refDataType)
                                    {
                                        // Render Reference Data Type options
                                        var gotoButtonWidth = 20;
                                        var refDropdownWidth = attribute.ColumnWidth - gotoButtonWidth;
                                        var currentValue = entryValue == null ? "..." : entryValue.ToString();
                                        if (DropdownButton(currentValue, refDropdownWidth, cellStyle.DropdownStyle))
                                        {
                                            var referenceEntryOptions = new GenericMenu();

                                            if (GetScheme(refDataType.ReferenceSchemeName)
                                                .Try(out var refSchema))
                                            {
                                                foreach (var identifierValue in refSchema.GetIdentifierValues())
                                                {
                                                    referenceEntryOptions.AddItem(new GUIContent(identifierValue.ToString()),
                                                        on: identifierValue.Equals(currentValue), () =>
                                                        {
                                                            _ = ExecuteSetDataOnEntryAsync(scheme, entry, attributeName, identifierValue);
                                                        });
                                                }
                                            }
                                            referenceEntryOptions.ShowAsContext();
                                        }
                                        
                                        if (GUILayout.Button("O", GUILayout.Width(gotoButtonWidth)))
                                        {
                                            FocusOnEntry(refDataType.ReferenceSchemeName, refDataType.ReferenceAttributeName, currentValue);
                                            GUI.FocusControl(null);
                                        }
                                    }
                                    else {
                                        entryValue = EditorGUILayout.TextField(entryValue == null ? string.Empty : entryValue.ToString(), cellStyle.FieldStyle, attributeFieldWidth);
                                    }
                                    int lastControlId = GUIUtility.GetControlID(FocusType.Passive);
                                    tableCellControlIds[entryIdx * attributeCount + attributeIdx] = lastControlId;

                                    if (changed.changed)
                                    {
                                        bool shouldUpdateEntry = false;
                                        if (attribute.IsIdentifier)
                                        {
                                            if (scheme.GetIdentifierValues()
                                                .Any(otherAttributeName =>
                                                    otherAttributeName.Equals(entryValue)))
                                            {
                                                EditorUtility.DisplayDialog("Schema", $"Attribute '{attribute.AttributeName}' with value '{entryValue}' already exists.", "OK");
                                            }
                                            else
                                            {
                                                shouldUpdateEntry = true;
                                                Debug.Log($"Setting unique attribute '{attribute.AttributeName}' to '{scheme.SchemeName}', value '{entryValue}'.");
                                            }
                                        }
                                        else
                                        {
                                            shouldUpdateEntry = true;
                                            Debug.Log($"Setting attribute '{attribute.AttributeName}' to '{scheme.SchemeName}'.");
                                        }

                                        if (shouldUpdateEntry)
                                        {
                                            if (attribute.IsIdentifier)
                                            {
                                                UpdateIdentifierValue(scheme.SchemeName, 
                                                    attributeName,
                                                    entry.GetData(attributeName), entryValue);
                                            }
                                            else
                                            {
                                                _ = ExecuteSetDataOnEntryAsync(scheme, entry, attributeName, entryValue);
                                            }
                                            
                                            // This operation can dirty multiple data schemes, save any affected
                                            LatestResponse = Save();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    entryIdx++;
                }

                GUILayout.EndScrollView();
                
                // add new entry form
                if (scheme.AttributeCount > 0)
                {
                    if (AddButton("Create New Entry", expandWidth: true, height: 50f))
                    {
                        scheme.CreateNewEntry();
                        Debug.Log($"Added entry to '{scheme.SchemeName}'.");
                    }
                }
            }
            
            // handle arrow key navigation of table
            var ev = Event.current;
            // Sometimes this receives multiple events but one doesn't contain a keycode?
            if (ev.type == EventType.KeyUp && ev.keyCode != KeyCode.None)
            {
                // IDK why this is off-by-one
                int focusedIndex = Array.IndexOf(tableCellControlIds, GUIUtility.keyboardControl + 1);
                
                if (focusedIndex != -1)
                {
                    // shift control focus and stay clamped to valid controls
                    int nextFocusedIndex = -1;
                    switch (ev.keyCode)
                    {
                        case KeyCode.UpArrow:
                            if (focusedIndex - attributeCount >= 0)
                            {
                                nextFocusedIndex = focusedIndex - attributeCount;
                            }
                            break;
                        case KeyCode.DownArrow:
                            if (focusedIndex + attributeCount < tableCellControlIds.Length)
                            {
                                nextFocusedIndex = focusedIndex + attributeCount;
                            }
                            break;
                        case KeyCode.LeftArrow:
                            if (focusedIndex % attributeCount > 0)
                            {
                                nextFocusedIndex = focusedIndex - 1;
                            }
                            break;
                        case KeyCode.Return:
                        case KeyCode.KeypadEnter:
                            // try to go right if possible
                            if (focusedIndex + 1 < tableCellControlIds.Length)
                            {
                                nextFocusedIndex = focusedIndex + 1;
                            }

                            break;
                        case KeyCode.RightArrow:
                            if (focusedIndex % attributeCount < attributeCount - 1)
                            {
                                nextFocusedIndex = focusedIndex + 1;
                            }
                            break;
                    }

                    if (nextFocusedIndex != -1)
                    {
                        // IDK why this is off-by-one
                        GUIUtility.keyboardControl = tableCellControlIds[nextFocusedIndex] - 1;
                        ev.Use(); // make sure to consume event if we used it
                    }
                }
            }
        }

        private void RenderTableHeader(int attributeCount, DataScheme scheme)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("#", RightAlignedLabelStyle, 
                    GUILayout.Width(SETTINGS_WIDTH),
                    GUILayout.ExpandWidth(false));
                
                // render column attribute headings
                for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                {
                    var attribute = scheme.GetAttribute(attributeIdx);
                        
                    // column settings gear
                    string attributeLabel = attribute.AttributeName;
                    if (attribute.IsIdentifier)
                    {
                        attributeLabel = $"{attribute.AttributeName} - ID";
                    }
                    else if (attribute.DataType is ReferenceDataType)
                    {
                        attributeLabel = $"{attribute.AttributeName} - Ref";
                    }

                    var sortOrder = GetSortOrderForScheme(scheme);
                    Texture attributeIcon = null;
                    if (sortOrder.AttributeName == attribute.AttributeName)
                    {
                        switch (sortOrder.Order)
                        {
                            case SortOrder.Ascending:
                                attributeIcon = EditorIcon.UpArrow;
                                break;
                            case SortOrder.Descending:
                                attributeIcon = EditorIcon.DownArrow;
                                break;
                        }
                    }
                    
                    var attributeContent = new GUIContent(attributeLabel,
                        attributeIcon,
                        attribute.AttributeToolTip);
                    if (DropdownButton(attributeContent, attribute.ColumnWidth))
                    {
                        RenderAttributeColumnOptions(attributeIdx, scheme, attribute, attributeCount);
                    }
                }

                // add new attribute form
                newAttributeName = GUILayout.TextField(newAttributeName, GUILayout.ExpandWidth(false), GUILayout.MinWidth(100));
                using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newAttributeName)))
                {
                    if (AddButton("Add Attribute"))
                    {
                        Debug.Log($"Added new attribute to '{scheme.SchemeName}'.`");
                        LatestResponse = scheme.AddAttribute(new AttributeDefinition
                        {
                            AttributeName = newAttributeName,
                            DataType = DataType.Text,
                            DefaultValue = string.Empty,
                            IsIdentifier = false,
                            ColumnWidth = AttributeDefinition.DefaultColumnWidth,
                        });
                        if (LatestResponse.Passed) 
                        {
                            LatestResponse = SaveDataScheme(scheme, alsoSaveManifest: false);
                        }
                        newAttributeName = string.Empty; // clear out attribute name field since it's unlikely someone wants to make another attribute with the same name
                    }
                }
            }
        }

        private void RenderEntryRowOptions(int entryIdx, int entryCount, DataScheme scheme, DataEntry entry)
        {
            var rowOptionsMenu = new GenericMenu();

            var sortOrder = GetSortOrderForScheme(scheme);
            bool canMoveUp = sortOrder.HasValue && entryIdx != 0;
            bool canMoveDown = sortOrder.HasValue && entryIdx != entryCount - 1;
            
            rowOptionsMenu.AddItem(new GUIContent("Move To Top"), isDisabled: canMoveUp, () =>
            {
                scheme.MoveEntry(entry, 0);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Up"), isDisabled: canMoveUp, () =>
            {
                scheme.MoveUpEntry(entry);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Down"), isDisabled: canMoveDown, () =>
            {
                scheme.MoveDownEntry(entry);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            rowOptionsMenu.AddItem(new GUIContent("Move To Bottom"), isDisabled: canMoveDown, () =>
            {
                scheme.MoveEntry(entry, entryCount - 1);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            rowOptionsMenu.AddSeparator("");
            rowOptionsMenu.AddItem(new GUIContent("Delete Entry"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Schema", "Are you s you want to delete this entry?", "Yes, delete this entry", "No, cancel"))
                {
                    LatestResponse = scheme.DeleteEntry(entry);
                    SaveDataScheme(scheme, alsoSaveManifest: false);
                }
            });
            rowOptionsMenu.ShowAsContext();
        }

        private void RenderAttributeColumnOptions(int attributeIdx, DataScheme scheme, AttributeDefinition attribute,
            int attributeCount)
        {
            var columnOptionsMenu = new GenericMenu();

            // attribute column ordering options
            columnOptionsMenu.AddItem(new GUIContent("Move Left"), isDisabled: attributeIdx == 0, () =>
            {
                scheme.IncreaseAttributeRank(attribute);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move Right"), isDisabled: attributeIdx == attributeCount - 1, () =>
            {
                scheme.DecreaseAttributeRank(attribute);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
                            
            // Sorting options
            columnOptionsMenu.AddSeparator("");
            
            var sortOrder = GetSortOrderForScheme(scheme);
            columnOptionsMenu.AddItem(new GUIContent("Sort Ascending"), 
                on: sortOrder.Equals(new AttributeSortOrder(attribute.AttributeName, SortOrder.Ascending)), 
                () =>
            {
                SetColumnSort(scheme, attribute, SortOrder.Ascending);
            });
            
            columnOptionsMenu.AddItem(new GUIContent("Sort Descending"), 
                on: sortOrder.Equals(new AttributeSortOrder(attribute.AttributeName, SortOrder.Descending)),
                () =>
            {
                SetColumnSort(scheme, attribute, SortOrder.Descending);
            });
            
            columnOptionsMenu.AddItem(new GUIContent("Clear Sort"), 
                isDisabled: !sortOrder.HasValue,
                () =>
            {
                SetColumnSort(scheme, attribute, SortOrder.None);
            });
                            
            columnOptionsMenu.AddSeparator("");
                            
            columnOptionsMenu.AddItem(new GUIContent("Column Settings..."), false, () =>
            {
                AttributeSettingsPrompt.ShowWindow(scheme, attribute);
            });
                            
            columnOptionsMenu.AddSeparator("");
                            
            // options to convert type
            foreach (var builtInType in DataType.BuiltInTypes)
            {
                bool isMatchingType = builtInType.Equals(attribute.DataType);
                columnOptionsMenu.AddItem(new GUIContent($"Convert Type/{builtInType.TypeName}"), 
                    isMatchingType,
                    () =>
                    {
                        LatestResponse =
                            scheme.ConvertAttributeType(attributeName: attribute.AttributeName,
                                newType: builtInType);
                        if (LatestResponse.Passed) 
                        {
                            LatestResponse = SaveDataScheme(scheme, alsoSaveManifest: false);
                        }
                    });
            }
                            
            columnOptionsMenu.AddSeparator("Convert Type");
            foreach (var schemeName in AllSchemes.OrderBy(s => s))
            {
                if (GetScheme(schemeName).Try(out var dataSchema) 
                    && dataSchema.GetIdentifierAttribute().Try(out var identifierAttribute))
                {
                    var referenceDataType = new ReferenceDataType(schemeName, identifierAttribute.AttributeName);
                    bool isMatchingType = referenceDataType.Equals(attribute.DataType);
                    columnOptionsMenu.AddItem(new GUIContent($"Convert Type/{referenceDataType.TypeName}"),
                        on: isMatchingType, 
                        () =>
                        {
                            LatestResponse =
                                scheme.ConvertAttributeType(attributeName: attribute.AttributeName,
                                    newType: referenceDataType);
                            if (LatestResponse.Passed) 
                            {
                                LatestResponse = SaveDataScheme(scheme, alsoSaveManifest: false);
                            }
                        });
                }
            }
                            
            columnOptionsMenu.AddSeparator("");
                            
            columnOptionsMenu.AddItem(new GUIContent("Delete Attribute"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Schema", $"Are you sure you want to delete this attribute: {attribute.AttributeName}?", "Yes, delete this attribute", "No, cancel"))
                {
                    LatestResponse = scheme.DeleteAttribute(attribute);
                    if (LatestResponse.Passed)
                    {
                        LatestResponse = SaveDataScheme(scheme, alsoSaveManifest: false);
                    }
                }
            });

            columnOptionsMenu.ShowAsContext();
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

        // NEW: Undo/Redo Panel
        private void DrawUndoRedoPanel()
        {
            _showUndoRedoPanel = EditorGUILayout.Foldout(_showUndoRedoPanel, "Undo/Redo Controls");
            if (!_showUndoRedoPanel) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!_commandHistory.CanUndo || _operationInProgress);
            if (GUILayout.Button($"Undo ({_commandHistory.UndoHistory.Count})", GUILayout.Width(100)))
            {
                _ = ExecuteUndoAsync();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_commandHistory.CanRedo || _operationInProgress);
            if (GUILayout.Button($"Redo ({_commandHistory.RedoHistory.Count})", GUILayout.Width(100)))
            {
                _ = ExecuteRedoAsync();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(_commandHistory.Count == 0 || _operationInProgress);
            if (GUILayout.Button("Clear History", GUILayout.Width(100)))
            {
                _commandHistory.ClearHistory();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        // NEW: Progress Bar rendering
        private void DrawProgressBar()
        {
            if (!_operationInProgress) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_currentOperationDescription ?? "Working...", GUILayout.Width(200));
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(rect, _currentProgress, _currentProgressMessage ?? string.Empty);
            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                CancelCurrentOperation();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        // NEW: Async helper methods for undo/redo
        private async Task ExecuteUndoAsync()
        {
            if (_operationInProgress) return;
            _operationInProgress = true;
            _currentOperationDescription = "Undoing last command";
            try
            {
                await _commandHistory.UndoAsync(_cancellationTokenSource.Token);
                // Persist sync save for now
                Schema.Core.Schema.Save();
            }
            catch (OperationCanceledException) { }
            finally
            {
                _operationInProgress = false;
                Repaint();
            }
        }

        private async Task ExecuteRedoAsync()
        {
            if (_operationInProgress) return;
            _operationInProgress = true;
            _currentOperationDescription = "Redoing last undone command";
            try
            {
                await _commandHistory.RedoAsync(_cancellationTokenSource.Token);
                Schema.Core.Schema.Save();
            }
            catch (OperationCanceledException) { }
            finally
            {
                _operationInProgress = false;
                Repaint();
            }
        }

        // NEW: Progress update callback (placeholder for future commands)
        private void UpdateProgress(CommandProgress progress)
        {
            _currentProgress = progress.Value;
            _currentProgressMessage = progress.Message;
            EditorApplication.delayCall += Repaint;
        }

        private void CancelCurrentOperation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _operationInProgress = false;
        }

        // NEW: executes SetDataOnEntryCommand through command history
        private async Task ExecuteSetDataOnEntryAsync(DataScheme scheme, DataEntry entry, string attributeName, object value)
        {
            if (_operationInProgress) return;
            _operationInProgress = true;
            _currentOperationDescription = $"Updating '{scheme.SchemeName}.{attributeName}'";
            try
            {
                var progress = new Progress<CommandProgress>(UpdateProgress);
                var cmd = new SetDataOnEntryCommand(scheme, entry, attributeName, value);
                var result = await _commandHistory.ExecuteAsync(cmd, _cancellationTokenSource.Token);
                if (!result.IsSuccess)
                {
                    Debug.LogError(result.Message);
                }
                else
                {
                    // Persist change
                    Schema.Core.Schema.Save();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _operationInProgress = false;
                _currentProgress = 0f;
                _currentProgressMessage = string.Empty;
                Repaint();
            }
        }
    }
}