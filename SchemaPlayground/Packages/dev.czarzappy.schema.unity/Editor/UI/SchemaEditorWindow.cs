using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Random = System.Random;
using Schema.Core.Commands;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Schemes;
using Schema.Unity.Editor.Ext;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;
using static Schema.Unity.Editor.SchemaLayout;
using Logger = Schema.Core.Logging.Logger;
using Object = UnityEngine.Object;

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
            _cancellationTokenSource = new CancellationTokenSource();
            // Subscribe to command history events for repainting
            _commandHistory.CommandExecuted += (_, __) => Repaint();
            _commandHistory.CommandUndone += (_, __) => Repaint();
            _commandHistory.CommandRedone += (_, __) => Repaint();
            
            // Initialize virtual scrolling
            _virtualTableView = new VirtualTableView();

            OnAttributeFiltersUpdated += RefreshTableEntriesForSelectedScheme;
            OnSelectedSchemeChanged += RefreshTableEntriesForSelectedScheme;
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
                    importFilePath: importFilePath,
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
                LogDbgVerbose("Add schema operation cancelled");
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
            LatestManifestLoadResponse = LoadManifestFromPath(ManifestImportPath);
            LatestResponse = CheckIf(LatestManifestLoadResponse.Passed, LatestManifestLoadResponse.Message, Context);
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
            
            showDebugView = EditorGUILayout.Foldout(showDebugView, "Debug View");
            if (showDebugView)
            {
                RenderDebugView();
            }
            
            InitializeStyles();
            debugIdx = 0;
            GUILayout.Label("Scheme Editor", EditorStyles.largeLabel, 
                ExpandWidthOptions);

            // NEW: Undo/Redo controls and progress display
            DrawUndoRedoPanel();
            DrawProgressBar();

            if (!LatestManifestLoadResponse.Passed)
            {
                EditorGUILayout.HelpBox("Hello! Would you like to Create an Empty Project or Load an Existing Project Manifest", MessageType.Info);
                if (GUILayout.Button("Start Empty Project"))
                {
                    // TODO: Handle this better? move to Schema Core?
                    LatestResponse = SaveManifest();
                    LatestManifestLoadResponse = SchemaResult<ManifestLoadStatus>.CheckIf(LatestResponse.Passed, 
                        ManifestLoadStatus.FULLY_LOADED, 
                        LatestResponse.Message,
                        "Loaded template manifest", Context);
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
                            
                            AddSchemaAsync(newSchema, importFilePath: relativePath).FireAndForget();
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
                            var scriptableObjectType = typeof(ScriptableObject);
                            var editorWindowType = typeof(EditorWindow);
                            var editorType = typeof(UnityEditor.Editor);
                            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                                .Where(a => !a.FullName.StartsWith("Unity.") && // filter Unity-related assembles.
                                            !a.FullName.StartsWith("UnityEngine") &&
                                            !a.FullName.StartsWith("UnityEditor") &&
                                            !a.FullName.Contains("Unity.Editor") &&
                                            !a.FullName.StartsWith("Schema")); // Filter Schema assemblies
                            var soTypes = assemblies.SelectMany(a => a.GetTypes())
                                .Where(t => scriptableObjectType.IsAssignableFrom(t) && 
                                            !(editorWindowType.IsAssignableFrom(t) || editorType.IsAssignableFrom(t))); // Editor and EditorWindow are Scriptable Objects :upsidedown-smile:

                            foreach (var soType in soTypes)
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
                                    AddSchemaAsync(importedSchema, importFilePath: importFilePath).FireAndForget();
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
            var serializedFieldAttribute = typeof(SerializeField);
            var serializedFields = soType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.GetCustomAttribute(serializedFieldAttribute) != null);

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
                            AddSchemaAsync(enumScheme, importFilePath: enumSchemeFileName).FireAndForget();
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
            AddSchemaAsync(dataScheme, importFilePath: fileName).FireAndForget();
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