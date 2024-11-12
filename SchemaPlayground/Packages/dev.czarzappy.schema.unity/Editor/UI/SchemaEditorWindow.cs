using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Core;
using Schema.Core.Serialization;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using static Schema.Core.SchemaResult;
using Logger = Schema.Core.Logger;
using Random = System.Random;

namespace Schema.Unity.Editor
{
    public class SchemeEditorWindow : EditorWindow
    {
        #region Static Fields and Constants

        private const string EDITORPREFS_KEY_SELECTEDSCHEME = "Schema:SelectedSchemeName";
        
        private static ProfilerMarker explorerViewMarker = new ProfilerMarker("SchemaEditor:ExplorerView");
        private static ProfilerMarker tableViewMarker = new ProfilerMarker("SchemaEditor:TableView");

        private const float SETTINGS_WIDTH = 50;

        private static bool SettingsButton(string text = "", float width = SETTINGS_WIDTH) => EditorGUILayout.DropdownButton(
            new GUIContent(text, EditorGUIUtility.IconContent(EditorIcon.GEAR_ICON_NAME).image),
            FocusType.Keyboard, GUILayout.MaxWidth(width));


        private bool DropdownButton(string text = "", float width = SETTINGS_WIDTH, GUIStyle style = null) =>
            DropdownButton(new GUIContent(text), width, style);
        
        private bool DropdownButton(GUIContent content, float width = SETTINGS_WIDTH, GUIStyle style = null)
        {
            var buttonStyle = style ?? defaultDropdownButtonStyle;
            return EditorGUILayout.DropdownButton(
                content,
                FocusType.Keyboard, buttonStyle, GUILayout.Width(width));
        }

        private static string DEFAULT_CONTENT_PATH;
        private static string DEFAULT_MANIFEST_LOAD_PATH;
        private FileSystemWatcher manifestWatcher;
        
        #endregion
        
        #region Fields and Properties

        private List<SchemaResult> responseHistory = new List<SchemaResult>();
        private SchemaResult LatestResponse
        {
            get => responseHistory.LastOrDefault();
            set => responseHistory.Add(value);
        }

        private Vector2 explorerScrollPosition;
        private Vector2 tableViewScrollPosition;

        [NonSerialized]
        private string newSchemeName = string.Empty;
        [NonSerialized]
        private string selectedSchemeName = string.Empty;
        [NonSerialized]
        private string newAttributeName = string.Empty;
        
        public string manifestFilePath = string.Empty;
        public string tooltipOfTheDay = string.Empty;
        
        [SerializeField]
        private int selectedSchemaIndex = -1;
        
        private GUIStyle leftAlignedButtonStyle;
        private GUIStyle rightAlignedLabelStyle;
        private GUIStyle defaultDropdownButtonStyle;
        
        [NonSerialized]
        private CellStyle cellEvenStyle;

        private static float evenOddsBase = 0.4f;
        private static float evenOddsOffset = 0.3f;
        private static readonly Color cellEventBackgroundColor = new Color(evenOddsBase, evenOddsBase, evenOddsBase);

        [NonSerialized]
        private CellStyle cellOddStyle;

        private static readonly Color cellOddBackgroundColor = new Color(evenOddsBase + evenOddsOffset, evenOddsBase + evenOddsOffset, evenOddsBase + evenOddsOffset);

        public class CellStyle
        {
            public GUIStyle FieldStyle { get; }
            public GUIStyle DropdownStyle { get; }
            public GUIStyle ButtonStyle { get; }
            private Color backgroundColor;
            public Color BackgroundColor
            {
                get => backgroundColor;
            }

            public CellStyle()
            {
                FieldStyle = new GUIStyle(EditorStyles.textField);
                DropdownStyle = new GUIStyle("MiniPullDown");
                ButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }

            public void SetBackgroundColor(Color backgroundColor)
            {
                Texture2D backgroundTexture = new Texture2D(1, 1);
                backgroundTexture.SetPixels(new[]
                {
                    backgroundColor
                });
                backgroundTexture.Apply();
                
                // FieldStyle.normal.background = backgroundTexture;
                
                // DropdownStyle.normal.background = backgroundTexture;
                
                // DropdownStyle.active.background = backgroundTexture;
                // DropdownStyle.hover.background = backgroundTexture;
                
                // ButtonStyle.normal.background = backgroundTexture;
                this.backgroundColor = backgroundColor;
            }
        }
        
        private DateTime lastChanged = DateTime.MinValue;
        private readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(500);
        
        #endregion

        #region Unity Lifecycle Methods
        
        [MenuItem("Tools/Scheme Editor")]
        public static void ShowWindow()
        {
            GetWindow<SchemeEditorWindow>("Scheme Editor");
        }

        private void OnEnable()
        {
            Debug.Log("Scheme Editor enabled");
            selectedSchemaIndex = -1;
            selectedSchemeName = string.Empty;
            newAttributeName = string.Empty;

            DEFAULT_CONTENT_PATH = Path.Combine(Path.GetFullPath(Application.dataPath + "/.."), "Content");
            DEFAULT_MANIFEST_LOAD_PATH = GetContentPath("Manifest.json");

            manifestFilePath = DEFAULT_MANIFEST_LOAD_PATH;
            LatestResponse = OnLoadManifest("On Editor Startup");
            if (LatestResponse.IsSuccess)
            {
                // manifest should be loaded at this point
                tooltipOfTheDay = $"Tip Of The Day: {GetTooltipMessage()}";

                InitializeFileWatcher();
            }

            var storedSelectedSchema = EditorPrefs.GetString(EDITORPREFS_KEY_SELECTEDSCHEME, null);
            if (!string.IsNullOrEmpty(storedSelectedSchema))
            {
                OnSelectScheme(storedSelectedSchema, "Restoring from Editor Preferences");
            }
        }
        

        private string GetContentPath(string schemeFileName)
        {
            return Path.Combine(DEFAULT_CONTENT_PATH, schemeFileName);
        }
        
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
                manifestWatcher.Changed += OnManifestChanged;
                manifestWatcher.EnableRaisingEvents = true;
            }
        }

        private void OnManifestChanged(object sender, FileSystemEventArgs e)
        {
            DateTime now = DateTime.Now;

            if (now - lastChanged < debounceTime) return;

            lastChanged = now;

            // Switch to main thread using UnityEditor.EditorApplication.delayCall
            EditorApplication.delayCall += () =>
            {
                // You can now safely interact with Unity API on the main thread here
                
                OnLoadManifest($"On Manifest File Changed ");
                Repaint();
            };
        }

        private void OnDisable()
        {
            manifestWatcher?.Dispose();
        }

        #endregion
        
        #region UI Command Handling

        private void AddSchema(DataScheme newSchema, string importFilePath = null)
        {
            bool overwriteExisting = false;
            if (Core.Schema.DoesSchemeExist(newSchema.SchemeName))
            {
                overwriteExisting = EditorUtility.DisplayDialog("Add Schema", $"A Schema named {newSchema.SchemeName} already exists. " +
                                                                              $"Do you want to overwrite this sceham>", "Yes", "No");
            }
            
            LatestResponse = Schema.Core.Schema.LoadDataScheme(newSchema, overwriteExisting, importFilePath: importFilePath);
            switch (LatestResponse.Status)
            {
                case RequestStatus.Failed:
                    Debug.LogError(LatestResponse.Payload);
                    break;
                case RequestStatus.Success:
                    Debug.Log($"New scheme '{newSchema.SchemeName}' created.");
                    OnSelectScheme(newSchema.SchemeName, "Added scheme");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnSelectScheme(string schemeName, string context)
        {
            Logger.Log($"Opening Schema '{schemeName}' for editing, {context}...");
            var schemeNames = Schema.Core.Schema.AllSchemes.ToArray();
            var prevSelectedIndex = Array.IndexOf(schemeNames, schemeName);
            if (prevSelectedIndex == -1)
            {
                return;
            }
            
            selectedSchemeName = schemeName;
            selectedSchemaIndex = prevSelectedIndex;
            EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, selectedSchemeName);
            newAttributeName = string.Empty;
        }

        private SchemaResult OnLoadManifest(string context)
        {
            using var progressReporter = new EditorProgressReporter("Schema", $"Loading Manifest - {context}");
            LatestResponse = Core.Schema.LoadFromManifest(manifestFilePath, progress: progressReporter);
            LatestManifestLoadResponse = LatestResponse;
            return LatestResponse;
        }

        public SchemaResult LatestManifestLoadResponse { get; set; }

        #endregion

        #region Rendering Methods
        
        private CellStyle GetRowCellStyle(int rowIdx)
        {
            switch (rowIdx % 2)
            {
                case 0:
                    return cellEvenStyle;
                case 1:
                default:
                    return cellOddStyle;
            }
        }

        private void InitializeStyles()
        {
            leftAlignedButtonStyle = new GUIStyle(GUI.skin.button);
            leftAlignedButtonStyle.alignment = TextAnchor.MiddleLeft;
            leftAlignedButtonStyle.padding = new RectOffset(10, 10, 5, 5);

            rightAlignedLabelStyle = new GUIStyle(GUI.skin.label);
            rightAlignedLabelStyle.alignment = TextAnchor.MiddleRight;
            
            defaultDropdownButtonStyle = new GUIStyle("MiniPullDown");
            defaultDropdownButtonStyle.alignment = TextAnchor.MiddleCenter;

            cellEvenStyle = new CellStyle();
            cellOddStyle = new CellStyle();
            
            cellEvenStyle.SetBackgroundColor(cellEventBackgroundColor);
            cellOddStyle.SetBackgroundColor(cellOddBackgroundColor);
        }

        private string GetTooltipMessage()
        {
            if (!Core.Schema.TryGetScheme("Tooltips", out var tooltips)) 
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

        private bool showDebugView = true;
        
        private void OnGUI()
        {
            showDebugView = EditorGUILayout.Foldout(showDebugView, "Debug View");
            if (showDebugView)
            {
                ShowDebugView();
            }
            
            InitializeStyles();
            debugIdx = 0;
            GUILayout.Label("Scheme Editor", EditorStyles.largeLabel, 
                GUILayout.ExpandWidth(true));

            if (!LatestManifestLoadResponse.IsSuccess)
            {
                EditorGUILayout.HelpBox("Hello! Would you like to Create an Empty Project or Load an Existing Project Manifest", MessageType.Info);
                if (GUILayout.Button("Start Empty Project"))
                {
                    LatestResponse = Core.Schema.SaveManifest(DEFAULT_MANIFEST_LOAD_PATH);
                    LatestManifestLoadResponse = LatestResponse;
                }
                return;
            }

            EditorGUILayout.HelpBox(tooltipOfTheDay, MessageType.Info);

            if (!LatestResponse.IsSuccess && LatestResponse.Payload != null)
            {
                EditorGUILayout.HelpBox(LatestResponse.Payload.ToString(), LatestResponse.MessageType());
            }

            GUILayout.BeginHorizontal();
            // Scrollable area to list existing schemes

            using (var _ = explorerViewMarker.Auto())
            {
                OnSchemaExplorerGUI();
            }

            DrawVerticalLine();
            using (var _ = tableViewMarker.Auto())
            {
                OnTableViewGUI();
            }
            // Table View
            GUILayout.EndHorizontal();
        }

        private void ShowDebugView()
        {
            using (new EditorGUI.DisabledScope())
            {
                EditorGUILayout.Toggle("Is Schema Initialized?", Core.Schema.IsInitialized);
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
                    LatestResponse = Core.Schema.SaveManifest(manifestFilePath);
                }
            }

            if (GUILayout.Button("Fix Duplicate Entries"))
            {
                foreach (var schemeName in Core.Schema.AllSchemes)
                {
                    if (!Core.Schema.TryGetScheme(schemeName, out var scheme))
                    {
                        continue; // can't de-dupe these schemes easily
                    }

                    if (!scheme.TryGetIdentifierAttribute(out var identifierAttribute))
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
                        Logger.LogWarning($"Scheme '{schemeName}' has deleted {numDeleted} entries.");
                        Core.Schema.SaveDataScheme(scheme, false);
                    }
                }
            }

            if (LatestResponse.Payload != null)
            {
                EditorGUILayout.HelpBox(LatestResponse.Payload.ToString(), LatestResponse.MessageType());
            }
        }

        private void OnSchemaExplorerGUI()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(400)))
            {
                GUILayout.Label($"Schema Explorer ({Schema.Core.Schema.AllSchemes.Count()} count):", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

                using (var explorerScrollView = new EditorGUILayout.ScrollViewScope(explorerScrollPosition))
                {
                    explorerScrollPosition = explorerScrollView.scrollPosition;
                    // list available schemes
                    var schemeNames = Schema.Core.Schema.AllSchemes.ToArray();

                    using (var schemeChange = new EditorGUI.ChangeCheckScope())
                    {
                        selectedSchemaIndex = GUILayout.SelectionGrid(selectedSchemaIndex, schemeNames, 1, leftAlignedButtonStyle);
                        
                        if (schemeChange.changed)
                        {
                            var nextSelectedSchema = schemeNames[selectedSchemaIndex];
                            OnSelectScheme(nextSelectedSchema, "Selected Schema in Explorer");
                        }
                    }
                
                    // New Schema creation form
                    EditorGUILayout.Space(10, false);
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
                }
            }
        }

        void OnTableViewGUI()
        {
            if (string.IsNullOrEmpty(selectedSchemeName))
            {
                EditorGUILayout.HelpBox("Select a Schema from the Schema Explorer to view in the table", MessageType.Info);
                return;
            }

            if (!Schema.Core.Schema.TryGetScheme(selectedSchemeName, out var scheme))
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
                        if (Core.Schema.TryGetManifestEntryForScheme(scheme, out var schemeManifest) && 
                            schemeManifest.TryGetDataAsString(Core.Schema.MANIFEST_ATTRIBUTE_FILEPATH, out storagePath))
                        {
                        }
                        
                        EditorGUILayout.TextField(storagePath);
                    }
                    if (GUILayout.Button("Save", GUILayout.ExpandWidth(true)))
                    {
                        LatestResponse = Core.Schema.SaveDataScheme(scheme, saveManifest: false);
                    }
                    
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
                
                // render table header
                using (new GUILayout.HorizontalScope())
                {
                    // GUILayoutUtility.GetRect(SETTINGS_WIDTH, 10, GUILayout.ExpandWidth(false));
                    EditorGUILayout.LabelField("#", rightAlignedLabelStyle, GUILayout.Width(SETTINGS_WIDTH), GUILayout.ExpandWidth(false));
                    // row settings gear spacing
                    
                    for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                    {
                        var attribute = scheme.GetAttribute(attributeIdx);
                        // GUILayout.Label(attribute.AttributeName, EditorStyles.boldLabel, GUILayout.MaxWidth(attribute.ColumnWidth - SETTINGS_WIDTH));
                        
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
                        
                        var attributeContent = new GUIContent(attributeLabel, attribute.AttributeToolTip);
                        if (DropdownButton(attributeContent, attribute.ColumnWidth))
                        {
                            ShowAttributeColumnOptions(attributeIdx, scheme, attribute, attributeCount);
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
                                DataType = DataType.String,
                                DefaultValue = string.Empty,
                                IsIdentifier = false,
                                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
                            });
                            if (LatestResponse.IsSuccess) 
                            {
                                LatestResponse = Core.Schema.SaveDataScheme(scheme, saveManifest: false);
                            }
                            newAttributeName = string.Empty; // clear out attribute name field since it's unlikely someone wants to make another attribute with the same name
                        }
                    }
                }
                
                // render table body, scheme data entries
                for (int entryIdx = 0; entryIdx < entryCount; entryIdx++)
                {
                    var entry = scheme.GetEntry(entryIdx);
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
                                ShowEntryRowOptions(entryIdx, entryCount, scheme, entry);
                            }
                            
                            for (int attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                            {
                                var attribute = scheme.GetAttribute(attributeIdx);
                                var attributeName = attribute.AttributeName;
                                var entryValue = entry.GetData(attributeName);
                                bool dataExists = entryValue != null;
                                if (!dataExists)
                                {
                                    // for some reason this data wasn't set yet
                                    var newData = attribute.CloneDefaultValue();
                                    entry.SetData(attributeName, newData);
                                    entryValue = newData;
                                }

                                var attributeFieldWidth = GUILayout.Width(attribute.ColumnWidth);
                                using (var changed = new EditorGUI.ChangeCheckScope())
                                {
                                    if (attribute.DataType == DataType.Integer)
                                    {
                                        entryValue = EditorGUILayout.IntField(Convert.ToInt32(entryValue), cellStyle.FieldStyle,
                                            attributeFieldWidth);
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
                                        var gotoButtonWidth = 20;
                                        var refDropdownWidth = attribute.ColumnWidth - gotoButtonWidth;
                                        var currentValue = entryValue == null ? "..." : entryValue.ToString();
                                        if (DropdownButton(currentValue, refDropdownWidth, cellStyle.DropdownStyle))
                                        {
                                            var referenceEntryOptions = new GenericMenu();

                                            if (Core.Schema.TryGetScheme(refDataType.ReferenceSchemeName,
                                                    out var refSchema))
                                            {
                                                foreach (var identifierValue in refSchema.GetIdentifierValues())
                                                {
                                                    referenceEntryOptions.AddItem(new GUIContent(identifierValue.ToString()),
                                                        on: identifierValue.Equals(currentValue), () =>
                                                        {
                                                            entry.SetData(attributeName, identifierValue);
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
                                    int lastControlId = EditorGUIUtility.GetControlID(FocusType.Passive);
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
                                            entry.SetData(attributeName, entryValue);
                                            LatestResponse = Core.Schema.SaveDataScheme(scheme, saveManifest: false);
                                        }
                                    }
                                }
                            }
                        }
                    }
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
                int focusedIndex = Array.IndexOf(tableCellControlIds, EditorGUIUtility.keyboardControl + 1);
                
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

        private void FocusOnEntry(string referenceSchemeName, string referenceAttributeName, string currentValue)
        {
            OnSelectScheme(referenceSchemeName, "Focus On Entry");
        }

        private void ShowEntryRowOptions(int entryIdx, int entryCount, DataScheme scheme, DataEntry entry)
        {
            var rowOptionsMenu = new GenericMenu();
            bool isFirstEntry = entryIdx == 0;
            bool isLastEntry = entryIdx == entryCount - 1;
            rowOptionsMenu.AddItem(new GUIContent("Move To Top"), isDisabled: isFirstEntry, () =>
            {
                scheme.MoveEntry(entry, 0);
                Core.Schema.SaveDataScheme(scheme, saveManifest: false);
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Up"), isDisabled: isFirstEntry, () =>
            {
                scheme.MoveUpEntry(entry);
                Core.Schema.SaveDataScheme(scheme, saveManifest: false);
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Down"), isDisabled: isLastEntry, () =>
            {
                scheme.MoveDownEntry(entry);
                Core.Schema.SaveDataScheme(scheme, saveManifest: false);
            });
            rowOptionsMenu.AddItem(new GUIContent("Move To Bottom"), isDisabled: isLastEntry, () =>
            {
                scheme.MoveEntry(entry, entryCount - 1);
                Core.Schema.SaveDataScheme(scheme, saveManifest: false);
            });
            rowOptionsMenu.AddSeparator("");
            rowOptionsMenu.AddItem(new GUIContent("Delete Entry"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Schema", "Are you s you want to delete this entry?", "Yes, delete this entry", "No, cancel"))
                {
                    LatestResponse = scheme.DeleteEntry(entry);
                    Core.Schema.SaveDataScheme(scheme, saveManifest: false);
                }
            });
            rowOptionsMenu.ShowAsContext();
        }

        private void ShowAttributeColumnOptions(int attributeIdx, DataScheme scheme, AttributeDefinition attribute,
            int attributeCount)
        {
            var columnOptionsMenu = new GenericMenu();

            // attribute column ordering options
            columnOptionsMenu.AddItem(new GUIContent("Move Left"), isDisabled: attributeIdx == 0, () =>
            {
                scheme.IncreaseAttributeRank(attribute);
                Core.Schema.SaveDataScheme(scheme, saveManifest: false);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move Right"), isDisabled: attributeIdx == attributeCount - 1, () =>
            {
                scheme.DecreaseAttributeRank(attribute);
                Core.Schema.SaveDataScheme(scheme, saveManifest: false);
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
                        if (LatestResponse.IsSuccess) 
                        {
                            LatestResponse = Core.Schema.SaveDataScheme(scheme, saveManifest: false);
                        }
                    });
            }
                            
            columnOptionsMenu.AddSeparator("Convert Type");
            foreach (var schemeName in Core.Schema.AllSchemes.OrderBy(s => s))
            {
                if (Core.Schema.TryGetScheme(schemeName, out var dataSchema) && dataSchema.TryGetIdentifierAttribute(out var identifierAttribute))
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
                            if (LatestResponse.IsSuccess) 
                            {
                                LatestResponse = Core.Schema.SaveDataScheme(scheme, saveManifest: false);
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
                }
            });

            columnOptionsMenu.ShowAsContext();
        }

        public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            r.height = thickness;
            r.y+=padding/2;
            r.x-=2;
            r.width +=6;
            EditorGUI.DrawRect(r, color);
        }

        private int debugIdx;
        private const bool allowUserToLoadAManifest = false;

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
    }
}