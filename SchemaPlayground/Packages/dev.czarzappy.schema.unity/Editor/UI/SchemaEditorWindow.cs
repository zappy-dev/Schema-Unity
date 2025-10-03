using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Random = System.Random;
using Schema.Core.Schemes;
using Schema.Runtime;
using Schema.Unity.Editor.Ext;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;
using static Schema.Unity.Editor.LayoutUtils;
using static Schema.Unity.Editor.SchemaLayout;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    [Serializable]
    internal partial class SchemaEditorWindow : EditorWindow
    {
        #region Static Fields and Constants

        public static SchemaEditorWindow Instance { get; private set; }

        private const string EDITORPREFS_KEY_SELECTEDSCHEME = "Schema:SelectedSchemeName";
        
        private static ProfilerMarker _explorerViewMarker = new ProfilerMarker("SchemaEditor:ExplorerView");
        private static ProfilerMarker _tableViewMarker = new ProfilerMarker("SchemaEditor:TableView");

        // TODO: Move to Schema Core?
        private static string _defaultManifestLoadPath;
        
        #endregion
        
        #region Fields and Properties
        
        private bool isInitialized;
        private bool showDebugView;
        
        public SchemaResult<ManifestLoadStatus> LatestManifestLoadResponse { get; set; }
        private List<SchemaResult> responseHistory = new List<SchemaResult>();
        private DateTime latestResponseTime;
        
        /// <summary>
        /// Latest Response from a user-initiated scheme action
        /// </summary>
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
            Instance = this;
            LogDbgVerbose("Scheme Editor enabled", this);
            isInitialized = false;
            EditorApplication.update += InitializeSafely;
            RegisterCommandHistoryCallbacks();
            
            // Initialize virtual scrolling
            _virtualTableView = new VirtualTableView();

            ManifestUpdated += RefreshTableEntriesForSelectedScheme;
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
            // Only unsubscribe if initialized failed? also prevent attempting to initialize more than once?
            EditorApplication.update -= InitializeSafely;
            LogDbgVerbose("InitializeSafely", this);
            
            selectedSchemaIndex = -1;
            SelectedSchemeName = string.Empty;
            newAttributeName = string.Empty;

            SetStorage(StorageFactory.GetEditorStorage());

            ProjectPath = Path.GetFullPath(Application.dataPath + "/..");
            // _defaultContentPath = Path.Combine(Path.GetFullPath(Application.dataPath + "/.."), "Content");
            _defaultManifestLoadPath = GetContentPath("Manifest.json");
            // if (!IsInitialized)
            // {
            ManifestImportPath = _defaultManifestLoadPath;
            // }
            // return;
            var ctx = new SchemaContext
            {
                Driver = "Editor_Initialization",
            };
            LatestResponse = OnLoadManifest(ctx);
            
            if (LatestResponse.Passed)
            {
                // manifest should be loaded at this point
                // TODO: Solve publishing Tooltips for schema itself, multiple schema contexts...
                // tooltipOfTheDay = $"Tip Of The Day: {GetTooltipMessage()}";

                InitializeFileWatcher();
            }

            var storedSelectedSchema = EditorPrefs.GetString(EDITORPREFS_KEY_SELECTEDSCHEME, null);
            // return;
            if (!string.IsNullOrEmpty(storedSelectedSchema))
            {
                LogDbgVerbose($"Selected schema found: {storedSelectedSchema}", this);
                OnSelectScheme(storedSelectedSchema, ctx);
            }

            isInitialized = true;
        }

        #region Manifest File Changing Handling
        
        // TODO: Migrate to separate file
        private void InitializeFileWatcher()
        {
            if (manifestWatcher == null)
            {
                LogDbgVerbose($"Initializing file watch for path: {Path.GetDirectoryName(ManifestImportPath)}", this);
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
            LogDbgVerbose($"Manifest File Changed");
            DateTime now = DateTime.Now;

            if (now - lastManifestReloadTime < debounceTime)
            {
                LogDbgWarning($"Skipping re-loading manifest, allowing again in {now - lastManifestReloadTime}");
                return;
            }

            lastManifestReloadTime = now;

            // Switch to main thread using UnityEditor.EditorApplication.delayCall
            EditorApplication.delayCall += () =>
            {
                // You can now safely interact with Unity API on the main thread here
                
                OnLoadManifest(new SchemaContext
                {
                    Driver = "Editor_Detected_Manifest_File_Change",
                });
                Repaint();
            };
        }
        #endregion

        #endregion
        
        #region UI Command Handling

        private void OnSelectScheme(string schemeName, SchemaContext context)
        {
            // Unfocus any selected control fields when selecting a new scheme
            ReleaseControlFocus();

            if (GetAllSchemes(context).Try(out var allSchemes))
            {
                var schemeNames = allSchemes.ToArray();
                var prevSelectedIndex = Array.IndexOf(schemeNames, schemeName);
                if (prevSelectedIndex == -1)
                {
                    return;
                }
                selectedSchemaIndex = prevSelectedIndex;
            }
            
            LogDbgVerbose($"Opening Schema '{schemeName}' for editing, {context}...");
            SelectedSchemeName = schemeName;
            EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, schemeName);
            newAttributeName = string.Empty;
            
            // Clear virtual scrolling cache when switching schemes
            _virtualTableView?.ClearCache();
        }

        private SchemaResult OnLoadManifest(SchemaContext context)
        {
            LogDbgVerbose($"Loading Manifest", context);
            LatestManifestLoadResponse = LoadManifestFromPath(context, ManifestImportPath);
            LatestResponse = LatestManifestLoadResponse.Cast();

            if (LatestManifestLoadResponse.Passed)
            {
                RunManifestMigrationWizard();
            }
            return LatestResponse;
        }

        class ManifestAttributeEqualityComparer : IEqualityComparer<AttributeDefinition>
        {
            internal static ManifestAttributeEqualityComparer Instance = new ManifestAttributeEqualityComparer();
            
            public bool Equals(AttributeDefinition a, AttributeDefinition b)
            {
                if (a == null && b == null)
                {
                    return true;
                }

                if (a == null || b == null)
                {
                    return false;
                }
                
                // check if same attribute name
                if (!Equals(a.AttributeName, b.AttributeName))
                {
                    return false;
                }
                
                return true;
            }

            public int GetHashCode(AttributeDefinition obj)
            {
                return obj.GetHashCode();
            }
        }

        private SchemaResult RunManifestMigrationWizard()
        {
            var context = new SchemaContext
            {
                Driver = "Manifest Migration Wizard"
            };
            // validate manifest is up-to-date with latest template
            var templateManifest = ManifestDataSchemeFactory.BuildTemplateManifestSchema(context, 
                SchemaRuntime.DEFAULT_SCRIPTS_PUBLISH_PATH, 
                Path.Combine(DefaultContentDirectory, "Manifest.json"));

            context.Scheme = templateManifest._;
            
            var loadedManifestAttributes = LoadedManifestScheme._.GetAttributes();
            var templateAttributes = templateManifest._.GetAttributes();
            // TODO: Should just check if attributes are equal...
            // also check if the manifest self entry is the same?
            // Manifest self entry may be difference due to project modifications..
            // When migrating, we should try to keep the project's modifications, while upgrading to any new changes..
            var loadedManifestSelfEntry = LoadedManifestScheme.GetSelfEntry(context).Result;
            var templateManifestSelfEntry = templateManifest.GetSelfEntry(context).Result;

            var loadedEntryData = loadedManifestSelfEntry._.ToDictionary();
            var templateEntryData = templateManifestSelfEntry._.ToDictionary();
            var newKeys = templateEntryData.Keys.Except(loadedEntryData.Keys);
            var overlapKeys = loadedEntryData.Keys.Intersect(templateEntryData.Keys);
            
            
            // 1. Check if there are any differences between existing attributes
            // 2. Check if there are any new attributes
            // TODO: Some smarter wizard to choose which properties to take during a migration, which to skip, and committing that a migration was resolved to prevent future messages
            if (loadedManifestAttributes.Where(attr => overlapKeys.Contains(attr.AttributeName))
                    .SequenceEqual(templateAttributes.Where(attr => overlapKeys.Contains(attr.AttributeName)), ManifestAttributeEqualityComparer.Instance) &&
                !newKeys.Any()) return Pass();
            
            // auto-report differences
            var diffReport = new StringBuilder();
            BuildDiffReport(context, diffReport, LoadedManifestScheme, templateManifest);
            bool shouldUpgrade = EditorUtility.DisplayDialog("Schema - Manifest Out-Of-Data",
                diffReport.ToString(), "Upgrade", "Skip");
            
            if (!shouldUpgrade) return Pass();

            var loadedAttributesLookup = loadedManifestAttributes.ToDictionary(a => a.AttributeName);
            var templateAttributesLookup = templateAttributes.ToDictionary(a => a.AttributeName);

            // Migrate existing attributes
            foreach (var kvp in loadedAttributesLookup)
            {
                var attributeName = kvp.Key;
                var loadedAttribute = kvp.Value;

                if (!templateAttributesLookup.TryGetValue(attributeName, out var templateAttribute))
                {
                    // Not in templates, maybe a project-defined manifest attribute?
                    continue;
                }

                // Is this flipped?...
                // need a better heuristic for handling migrations...
                // maybe need to remember the previous manifest values?
                loadedAttribute.ColumnWidth = templateAttribute.ColumnWidth;
                loadedAttribute.AttributeToolTip = templateAttribute.AttributeToolTip;
                loadedAttribute.ShouldPublish = templateAttribute.ShouldPublish;
                loadedAttribute.IsIdentifier = templateAttribute.IsIdentifier;
                loadedAttribute.DefaultValue = templateAttribute.CloneDefaultValue();
                loadedAttribute.DataType = templateAttribute.DataType.Clone() as DataType;
                
            }
            
            if (!LoadedManifestScheme.GetEntries(context: context).Try(out var entries, out var entriesErr)) return entriesErr.Cast();

            var migrateRes = BulkResult(
                entries: entries,
                operation: (entry) =>
                {
                    // special case handling for the manifest scheme self entry...
                    if (entry.SchemeName == Manifest.MANIFEST_SCHEME_NAME)
                    {
                        if (templateManifest.GetSelfEntry(context).Try(out var templateManifestEntry))
                        {
                            
                            // Core.Schema.UpdateIdentifierValue(context, Manifest.MANIFEST_SCHEME_NAME, entry.SchemeName, )
                            // templateManifest._.ident
                            // manifestEntry
                            // manifestEntry.SchemeName = entry.SchemeName;
                            templateManifestEntry.CSharpExportPath = entry.CSharpExportPath;
                            templateManifestEntry.CSharpNamespace = entry.CSharpNamespace;
                            templateManifestEntry.FilePath = entry.FilePath;
                            templateManifestEntry.PublishTarget = entry.PublishTarget;
                            // TODO: Make sure to update for future parameters? Code gen?
                        }
                        // skip manifest...
                        // TODO: What manifest changes could exist in a project's manifest?
                        // Additional attribute values?
                        // merge new changes, with 
                        return Pass();
                    }
                    else
                    {
                        // entries containing data for attributes that do not exist...
                        return templateManifest._.AddEntry(context, entry._, runDataValidation: false);
                    }
                },
                errorMessage: "Failed to migrate existing entries.",
                context: context);

            if (migrateRes.Failed)
            {
                EditorUtility.DisplayDialog("Schema - Manifest Migration Failed", migrateRes.Message, "Ok");
                return migrateRes;
            }

            LatestManifestLoadResponse = LoadManifest(context, templateManifest._);
            LatestResponse = CheckIf(context, LatestManifestLoadResponse.Passed, LatestManifestLoadResponse.Message);
            return Pass();
        }

        /// <summary>
        /// Builds a human-readable report of differences between two manifests' attribute sets.
        /// Reports: added, removed, and modified attributes (per-field deltas).
        /// </summary>
        private void BuildDiffReport(SchemaContext context, StringBuilder diffReport, ManifestScheme currentManifest, ManifestScheme templateManifest)
        {
            var currentAttributes = currentManifest._.GetAttributes().ToDictionary(a => a.AttributeName);
            var templateAttributes = templateManifest._.GetAttributes().ToDictionary(a => a.AttributeName);

            diffReport.AppendLine("Your project is using an out-of-date Manifest version. Would you like to upgrade?");
            diffReport.AppendLine();

            // Removed (exist in current, not in template)
            var removed = currentAttributes.Keys.Except(templateAttributes.Keys).OrderBy(k => k).ToList();
            if (removed.Count > 0)
            {
                diffReport.AppendLine("Removed attributes:");
                foreach (var name in removed)
                {
                    var a = currentAttributes[name];
                    diffReport.AppendLine($"\t- {a.AttributeName} ({a.DataType.TypeName})");
                }
                diffReport.AppendLine();
            }

            // Added (exist in template, not in current)
            var added = templateAttributes.Keys.Except(currentAttributes.Keys).OrderBy(k => k).ToList();
            if (added.Count > 0)
            {
                diffReport.AppendLine("Added attributes:");
                foreach (var name in added)
                {
                    var b = templateAttributes[name];
                    diffReport.AppendLine($"\t+ {b.AttributeName} ({b.DataType.TypeName})");
                }
                diffReport.AppendLine();
            }

            // Modified (exist in both by name but differ in fields)
            var common = currentAttributes.Keys.Intersect(templateAttributes.Keys).OrderBy(k => k);
            var anyModified = false;
            foreach (var name in common)
            {
                var a = currentAttributes[name];
                var b = templateAttributes[name];

                if (a.Equals(b))
                {
                    continue;
                }

                anyModified = true;
                diffReport.AppendLine($"Modified attribute: {name}");

                if (!a.DataType.Equals(b.DataType))
                {
                    // TODO: More in-depth attribute diff report?
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.DataType)}: {a.DataType?.TypeName} -> {b.DataType?.TypeName}");
                }

                if (a.IsIdentifier != b.IsIdentifier)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.IsIdentifier)}: {a.IsIdentifier} -> {b.IsIdentifier}");
                }

                if (a.ShouldPublish != b.ShouldPublish)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.ShouldPublish)}: {a.ShouldPublish} -> {b.ShouldPublish}");
                }

                // UI fields (informational)
                if (a.AttributeToolTip != b.AttributeToolTip)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.AttributeToolTip)}: '{a.AttributeToolTip}' -> '{b.AttributeToolTip}'");
                }

                if (a.ColumnWidth != b.ColumnWidth)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.ColumnWidth)}: {a.ColumnWidth} -> {b.ColumnWidth}");
                }

                // Default value comparison (best-effort string rep)
                var aDefault = a.DefaultValue?.ToString();
                var bDefault = b.DefaultValue?.ToString();
                if (!string.Equals(aDefault, bDefault, StringComparison.Ordinal))
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.DefaultValue)}: '{aDefault}' -> '{bDefault}'");
                }

                // Reference data type details if applicable
                if (a.DataType is ReferenceDataType ar && b.DataType is ReferenceDataType br)
                {
                    if (ar.ReferenceSchemeName != br.ReferenceSchemeName)
                    {
                        diffReport.AppendLine($"\tReference Scheme: {ar.ReferenceSchemeName} -> {br.ReferenceSchemeName}");
                    }
                    if (ar.ReferenceAttributeName != br.ReferenceAttributeName)
                    {
                        diffReport.AppendLine($"\tReference Attribute: {ar.ReferenceAttributeName} -> {br.ReferenceAttributeName}");
                    }
                    if (ar.SupportsEmptyReferences != br.SupportsEmptyReferences)
                    {
                        diffReport.AppendLine($"\tAllow Empty Refs: {ar.SupportsEmptyReferences} -> {br.SupportsEmptyReferences}");
                    }
                }

                diffReport.AppendLine();
            }

            if (!anyModified && removed.Count == 0 && added.Count == 0)
            {
                var currentSelfEntry = currentManifest.GetSelfEntry(context).Result;
                var templateSelfEntry = templateManifest.GetSelfEntry(context).Result;
                if (Equals(currentSelfEntry, templateSelfEntry)) // TODO: acceptable modifications...
                {
                    diffReport.AppendLine("No differences detected.");
                }
                else
                {
                    diffReport.AppendLine("Manifest Self Entry is out-of-date.");
                    var currentEntryData = currentSelfEntry._.ToDictionary();
                    var templateEntryData = templateSelfEntry._.ToDictionary();
                    
                    var overlapKeys = currentEntryData.Keys.Intersect(templateEntryData.Keys).OrderBy(k => k).ToList();

                    foreach (var overlapKey in overlapKeys)
                    {
                        var currentValue = currentEntryData[overlapKey];
                        var templateValue = templateEntryData[overlapKey];
                        if (!Equals(currentValue, templateValue))
                        {
                            diffReport.AppendLine($"- '{overlapKey}': '{currentValue}' => '{templateValue}'");
                        }
                    }
                    
                    var currentOnlyKeys =  currentEntryData.Keys.Except(overlapKeys).OrderBy(k => k);
                    var templateOnlyKeys =  templateEntryData.Keys.Except(overlapKeys).OrderBy(k => k);

                    foreach (var currentOnlyKey in currentOnlyKeys)
                    {
                        diffReport.AppendLine($"- DELETED KEY: {currentOnlyKey}");
                    }
                    foreach (var templateOnlyKey in templateOnlyKeys)
                    {
                        diffReport.AppendLine($"- NEW KEY: {templateOnlyKey}");
                    }
                }
            }
        }
        
        private void SetColumnSort(DataScheme scheme, AttributeDefinition attribute, SortOrder sortOrder)
        {
            LogDbgVerbose($"Set column sort '{sortOrder}' for schema '{scheme.SchemeName}'.", this);
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

        private void FocusOnEntry(SchemaContext ctx, string referenceSchemeName, string referenceAttributeName, string currentValue)
        {
            OnSelectScheme(referenceSchemeName, ctx);
        }

        #endregion

        #region Rendering Methods
        private string GetTooltipMessage(SchemaContext context)
        {
            // TODO: Need to split the Schema Instance providing Schema's self data from the one used for a Project... different scopes.
            // May need to consider this for the runtime as well.
            if (!GetScheme(context, "Tooltips").Try(out var tooltipDataScheme)) 
                return "Could not find Tooltips scheme";

            var tooltips = new TooltipsScheme(tooltipDataScheme);
            
            int entriesCount = tooltips.EntryCount;
            if (entriesCount == 0)
            {
                return "No tooltips found.";
            }

            Random random = new Random();
            var randomIdx = random.Next(entriesCount);
            return tooltips.GetEntryByIndex(randomIdx).Message;
        }
        
        
        private void OnGUI()
        {
            var renderCtx = new SchemaContext
            {
                Driver = $"{nameof(SchemaEditorWindow)}_Render"
            };
            
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
                    var ctx = new SchemaContext
                    {
                        Driver = "User_Create_New_Project"
                    };
                    InitializeTemplateManifestScheme(ctx, SchemaRuntime.DEFAULT_SCRIPTS_PUBLISH_PATH);
                    LatestResponse = SaveManifest(ctx);
                    LatestManifestLoadResponse = SchemaResult<ManifestLoadStatus>.CheckIf(LatestResponse.Passed, 
                        ManifestLoadStatus.FULLY_LOADED, 
                        LatestResponse.Message,
                        "Loaded template manifest", ctx);
                }
            }
            
            EditorGUILayout.TextField("Project Path", ProjectPath);
            
            if (LatestManifestLoadResponse.Message != null &&
                LatestManifestLoadResponse.Failed)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestManifestLoadResponse.Status}: {LatestManifestLoadResponse.Message}", LatestManifestLoadResponse.MessageType());
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope())
                {
                    if (isInitialized)
                    {
                        string path = string.Empty;
#if SCHEMA_DEBUG
                        path = $"({RuntimeHelpers.GetHashCode(LoadedManifestScheme._)}) {ManifestImportPath}";
#else
                    path = ManifestImportPath;
#endif
                        
                        EditorGUILayout.TextField("Manifest Path", path);
                    }
                }

                if (GUILayout.Button("Load", DoNotExpandWidthOptions))
                {
                    OnLoadManifest(new SchemaContext
                    {
                        Driver = "User_Load_Manifest"
                    });
                }

                if (GUILayout.Button("Open", DoNotExpandWidthOptions))
                {
                    EditorUtility.RevealInFinder(ManifestImportPath);
                }

                if (GUILayout.Button("Save All", DoNotExpandWidthOptions))
                {
                    LatestResponse = Save(new SchemaContext
                    {
                        Driver = "User_Request_Save_All"
                    }, saveManifest: true);
                }
                
                if (GUILayout.Button("Publish All", DoNotExpandWidthOptions))
                {
                    LatestResponse = PublishAllSchemes(new SchemaContext
                    {
                        Driver = "User_Request_Publish_All"
                    });
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

            EditorGUILayout.TextField("Current Event", Event.current.ToString());
            EditorGUILayout.Vector2Field("Mouse Pos", Event.current.mousePosition);
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
                RenderSchemaExplorer(renderCtx);
            }

            DrawVerticalLine();
            using (var _ = _tableViewMarker.Auto())
            {
                RenderTableView(renderCtx);
            }
            // Table View
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(nextControlToFocus))
            {
                Logger.LogDbgVerbose($"Setting focus on control: {nextControlToFocus}");
                GUI.FocusControl(nextControlToFocus);
                nextControlToFocus = null;
            }

            if (releaseControl)
            {
                Logger.LogDbgVerbose($"Releasing focus");
                GUI.FocusControl(nextControlToFocus);
                releaseControl = false;
            }
        }

        /// <summary>
        /// Renders the Schema sidebar to view and interact with various Schema datatypes
        /// </summary>
        private void RenderSchemaExplorer(SchemaContext ctx)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(400)))
            {
                if (GetNumAvailableSchemes(ctx).Try(out var numSchemes))
                {
                    GUILayout.Label($"Schema Explorer ({numSchemes} count):", EditorStyles.boldLabel, DoNotExpandWidthOptions);

                }
                // New Schema creation form
                using (new EditorGUILayout.HorizontalScope(DoNotExpandWidthOptions))
                {
                    LogEvent(new Event(Event.current));
                    // Input field to add a new scheme

                    if (AddButton("Create New Schema"))
                    {
                        CreateNewSchemeWizard();
                    }

                    if (Event.current.type == EventType.Repaint)
                    {
                        // var r = GUILayoutUtility.GetLastRect();              // group-space
                        // var screenTL = GUIUtility.GUIToScreenPoint(r.position);
                        // var winTL    = (Vector2)position.position;           // window top-left in screen coords
                        // CreateNewSchemeButtonWinRect = new Rect(screenTL - winTL, r.size);
                        // CreateNewSchemeButtonWinRect = new Rect(r);
                        CreateNewSchemeButtonWinRect = this.GetLastScreenRect();
                        // Logger.LogDbgVerbose($"rect capture, r: {r}, {nameof(CreateNewSchemeButtonWinRect)}: {CreateNewSchemeButtonWinRect}");
                    }
                }
                
                EditorGUILayout.Space(10, false);
                    
                // render import options
                if (EditorGUILayout.DropdownButton(new GUIContent("Import"), 
                        FocusType.Keyboard, DoNotExpandWidthOptions))
                {
                    GenericMenu menu = new GenericMenu();

                    if (GetStorage(ctx).Try(out var storage))
                    {
                        foreach (var storageFormat in storage.AllFormats)
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
                                        ImportScriptableObjectToDataScheme(new SchemaContext
                                        {
                                            Driver = "User_Import_ScriptableObject"
                                        }, soType);
                                    });
                                }
                            }
                            else
                            {
                                menu.AddItem(new GUIContent(storageFormat.DisplayName), false, () =>
                                {
                                    var ctx = new SchemaContext
                                    {
                                        Driver = "User_Import_Schema"
                                    };
                                    if (storageFormat.TryImport(ctx, out var importedSchema, out var importFilePath))
                                    {
                                        SubmitAddSchemeRequest(ctx, importedSchema, importFilePath: importFilePath).FireAndForget();
                                    }
                                });
                            }
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
                    
                    var schemeNames = GetSchemes(ctx)
                        .Select(s => (DisplayName: DisplayName(s), SchemeName: s.SchemeName, Scheme: s)).ToArray();

                    using (var schemeChange = new EditorGUI.ChangeCheckScope())
                    {
                        selectedSchemaIndex = GUILayout.SelectionGrid(selectedSchemaIndex, schemeNames.Select(s =>
                        {
#if SCHEMA_DEBUG
                            return $"{s.DisplayName} ({RuntimeHelpers.GetHashCode(s.Scheme)})";
#else
                            return s.DisplayName;
#endif
                        }).ToArray(), 1, LeftAlignedButtonStyle);


                        if (Event.current.type == EventType.Repaint)
                        {
                            ExplorerWinRect = this.GetLastScreenRect();
                            // Logger.LogDbgVerbose($"ExplorerWinRect: {ExplorerWinRect}");
                        }

                        OverlapChecker(nameof(ExplorerWinRect), ExplorerWinRect);

                        if (schemeChange.changed)
                        {
                            var nextSelectedSchema = schemeNames[selectedSchemaIndex];
                            OnSelectScheme(nextSelectedSchema.SchemeName, ctx);
                        }
                    }
                }
            }
        }

        private void CreateNewSchemeWizard()
        {
            // EditorUtility.dial
            var wizardWindow = GetWindow<CreateNewSchemeWizardWindow>(utility: true, "Schema - Create New Scheme Wizard", focus: true);
            wizardWindow.ShowUtility();
        }

        private void LogEvent(Event current)
        {
            // Logger.LogDbgVerbose($"[{Time.frameCount}] Event.current: {Event.current}, isKey: {Event.current.isKey}");
            eventHistory.Add((Time.frameCount, current));
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
        private SchemaResult ImportScriptableObjectToDataScheme(SchemaContext context, Type soType)
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
            var assetGUIDAttrRes = dataScheme.AddAttribute(context, "Asset", DataType.Guid, isIdentifier: true);
            if (!assetGUIDAttrRes.Try(out var assetGuidAttr))
            {
                LogError(assetGUIDAttrRes.Message, assetGUIDAttrRes.Context);
                return assetGUIDAttrRes.Cast();
            }
            
            if (!GetStorage(context).Try(out var storage, out var storageErr)) return storageErr.Cast();
            
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
                    if (!GetScheme(context, enumSchemeName).Try(out var enumScheme))
                    {
                        if (EditorUtility.DisplayDialog(operationTitle,
                                $"No existing data scheme for enum '{enumSchemeName}'. Do you want to create one?", "Yes, create new Scheme", "Skip"))
                        {
                            // Create a new data scheme for referenced enum
                            enumScheme = new DataScheme(enumSchemeName);
                            // Create ID attribute to reference
                            var enumIdAttrRes = enumScheme.AddAttribute(context, "ID", DataType.Text, isIdentifier: true);
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
                                enumDataEntry.SetData(context, enumIdAttr.AttributeName, enumValue.ToString());
                                var addEntryRes = enumScheme.AddEntry(context, enumDataEntry);
                                if (addEntryRes.Failed)
                                {
                                    LogError(addEntryRes.Message, addEntryRes.Context);
                                    continue;
                                }
                            }
                                                    
                            // finally save new data scheme
                            string enumSchemeFileName = $"{enumSchemeName}.{storage.DefaultSchemaStorageFormat.Extension}";
                            SubmitAddSchemeRequest(context, enumScheme, importFilePath: enumSchemeFileName).FireAndForget();
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
                    dataScheme.AddAttribute(context, field.Name, dataType);
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
                var setAssetGuidRes = dataEntry.SetData(context, assetGuidAttr.AttributeName, assetGuid);

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
                            return ReferenceEquals(otherObject, entryValue);
                        });

                        if (otherAsset.dataEntry != default)
                        {
                            // TODO: fix this... the reference type itself is a guid...
                            // should this value match the reference's attribute (i.e a guid?)
                            entryValue = otherAsset.dataEntry.GetDataDirect(assetGuidAttr);
                            LogDbgVerbose($"Setting reference: {entryValue}");
                        }
                    }
                    var setDataRes = dataEntry.SetData(context, field.Name, entryValue);
                    if (setDataRes.Failed)
                    {
                        LogError(setDataRes.Message, setDataRes.Context);
                    }
                }

                var res = dataScheme.AddEntry(context, dataEntry, runValidation);
                if (res.Failed)
                {
                    Logger.LogError(res.Message, res.Context);
                    continue;
                }
            }

            progress.Progress($"Loading final Data Scheme", 0.9f);
            string fileName = $"{newSOSchemeName}.{storage.DefaultSchemaStorageFormat.Extension}";
            SubmitAddSchemeRequest(context, dataScheme, importFilePath: fileName).FireAndForget();
            return Pass("Submitted request to add new scheme");
        }
    }
}