using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Core;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;

namespace Schema.Unity.Editor
{
    public class SchemeEditorWindow : EditorWindow
    {
        private SchemaResponse latestResponse;
        private Vector2 explorerScrollPosition;
        private Vector2 tableViewScrollPosition;

        [NonSerialized]
        private string newSchemeName = string.Empty;
        [NonSerialized]
        private string selectedSchemaName = string.Empty;
        [NonSerialized]
        private string newAttributeName = string.Empty;
        
        [SerializeField]
        private int selectedSchemaIndex = -1;

        [MenuItem("Tools/Scheme Editor")]
        public static void ShowWindow()
        {
            GetWindow<SchemeEditorWindow>("Scheme Editor");
        }

        private void OnEnable()
        {
            Debug.Log("Scheme Editor enabled");
            selectedSchemaIndex = -1;
            selectedSchemaName = string.Empty;
            newAttributeName = string.Empty;
        }

        private static ProfilerMarker explorerViewMarker = new ProfilerMarker("SchemaEditor:ExplorerView");
        private static ProfilerMarker tableViewMarker = new ProfilerMarker("SchemaEditor:TableView");
        private void OnGUI()
        {
            GUILayout.Label("Scheme Editor", EditorStyles.boldLabel);

            if (latestResponse.Payload != null)
            {
                EditorGUILayout.HelpBox(latestResponse.Payload.ToString(), latestResponse.MessageType());
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

        private void OnSchemaExplorerGUI()
        {
            using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false)))
            {
                GUILayout.Label($"Schema Explorer ({Schema.Core.Schema.DataSchemes.Count} count):", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                explorerScrollPosition = GUILayout.BeginScrollView(explorerScrollPosition, GUILayout.Width(200), GUILayout.ExpandWidth(false));

                // list available schemas
                var schemaNames = Schema.Core.Schema.DataSchemes.Keys.ToArray();

                using (var schemaChange = new EditorGUI.ChangeCheckScope())
                {
                    selectedSchemaIndex = GUILayout.SelectionGrid(selectedSchemaIndex, schemaNames, 1);
                    
                    if (schemaChange.changed)
                    {
                        var nextSelectedSchema = schemaNames[selectedSchemaIndex];
                        OnSelectSchema(nextSelectedSchema);
                    }
                }
            
                // New Schema creation form
                GUILayout.Space(10);
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
                {
                    // Input field to add a new scheme
                    newSchemeName = EditorGUILayout.TextField( newSchemeName, GUILayout.ExpandWidth(false));

                    using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newSchemeName)))
                    {
                        if (AddButton("Add Schema"))
                        {
                            var newSchema = new DataScheme(newSchemeName);
                            AddSchema(newSchema);
                        }
                    }
                }
                
                GUILayout.Space(10);
                if (GUILayout.Button("Import From CSV", GUILayout.ExpandWidth(false)))
                {
                    var importedSchema = ImportFromCSV();
                    AddSchema(importedSchema);
                }

                GUILayout.EndScrollView();
            }
        }

        private void AddSchema(DataScheme newSchema)
        {
            latestResponse = Schema.Core.Schema.AddSchema(newSchema);
            switch (latestResponse.Status)
            {
                case RequestStatus.Error:
                    Debug.LogError(latestResponse.Payload);
                    break;
                case RequestStatus.Success:
                    newSchemeName = newSchema.SchemeName;
                    Debug.Log($"New scheme '{newSchemeName}' created.");
                    OnSelectSchema(newSchemeName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnSelectSchema(string schemaName)
        {
            Debug.Log($"Opening {schemaName} for editing...");
            selectedSchemaName = schemaName;
            newAttributeName = string.Empty;
        }

        void OnTableViewGUI()
        {
            if (string.IsNullOrEmpty(selectedSchemaName))
            {
                EditorGUILayout.HelpBox("Select a Schema from the Schema Explorer to view in the table", MessageType.Info);
                return;
            }

            if (!Schema.Core.Schema.DataSchemes.TryGetValue(selectedSchemaName, out var schema))
            {
                EditorGUILayout.HelpBox($"Schema '{selectedSchemaName}' does not exist.", MessageType.Warning);
                return;
            }
            
            int attributeCount = schema.Attributes.Count;
            int entryCount = schema.Entries.Count;
                
            // mapping entries to control IDs for focus/navigation management
            int[] controlIds = new int[attributeCount * entryCount];

            using (new GUILayout.VerticalScope())
            {
                GUILayout.Label($"Table View - {schema.SchemeName}", EditorStyles.boldLabel);

                if (GUILayout.Button("Export to CSV", GUILayout.ExpandWidth(false)))
                {
                    ExportToCSV(schema);
                }
                    
                tableViewScrollPosition = GUILayout.BeginScrollView(tableViewScrollPosition, alwaysShowHorizontal: true, alwaysShowVertical: true);
                
                // render columns
                using (new GUILayout.HorizontalScope())
                {
                    for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                    {
                        var attribute = schema.Attributes[attributeIdx];
                        GUILayout.Label(attribute.AttributeName, EditorStyles.boldLabel, GUILayout.MaxWidth(90));
                        if (EditorGUILayout.DropdownButton(new GUIContent(attribute.DataType.TypeName),
                                FocusType.Keyboard, GUILayout.MaxWidth(60)))
                        {
                            GenericMenu menu = new GenericMenu();

                            foreach (var builtInType in DataType.BuiltInTypes)
                            {
                                menu.AddItem(new GUIContent(builtInType.TypeName), builtInType == attribute.DataType,
                                    () =>
                                    {
                                        latestResponse =
                                            schema.ConvertAttributeType(attributeName: attribute.AttributeName,
                                                newType: builtInType);
                                    });
                            }

                            menu.ShowAsContext();
                        }
                    }

                    newAttributeName = GUILayout.TextField(newAttributeName, GUILayout.ExpandWidth(false), GUILayout.MinWidth(100));

                    using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newAttributeName)))
                    {
                        if (AddButton("Add Attribute"))
                        {
                            Debug.Log($"Added new attribute to '{schema.SchemeName}'.`");
                            latestResponse = schema.AddAttribute(newAttributeName, DataType.String, string.Empty);
                        }
                    }
                }
                
                // render data entries
                for (int entryIdx = 0; entryIdx < entryCount; entryIdx++)
                {
                    var entry = schema.Entries[entryIdx];
                    using (new GUILayout.HorizontalScope())
                    {
                        for (int attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                        {
                            var attribute = schema.Attributes[attributeIdx];
                            var attributeName = attribute.AttributeName;
                            var entryValue = entry.EntryData[attributeName];
                            using (var changed = new EditorGUI.ChangeCheckScope())
                            {
                                entryValue = GUILayout.TextField(entryValue.ToString(), DEFAULT_ENTRY_WIDTH);
                                int lastControlId = EditorGUIUtility.GetControlID(FocusType.Passive);
                                // log control ids
                                // Debug.Log($"({entryIdx},{attributeIdx}): {lastControlId}");
                                controlIds[entryIdx * attributeCount + attributeIdx] = lastControlId;

                                if (changed.changed)
                                {
                                    entry.EntryData[attributeName] = entryValue;
                                }
                            }
                        }
                    }
                }

                if (AddButton("Add Entry"))
                {
                    schema.CreateNewEntry();
                    Debug.Log($"Added entry to '{schema.SchemeName}'.`");
                }
                GUILayout.EndScrollView();
            }
            
            // handle arrow key navigation of table
            var ev = Event.current;
            // Sometimes this receives multiple events but one doesn't contain a keycode?
            if (ev.type == EventType.KeyUp && ev.keyCode != KeyCode.None)
            {
                if (ev.keyCode == KeyCode.Space)
                {
                    var sb = new StringBuilder();
                    for (var index = 0; index < controlIds.Length; index++)
                    {
                        if (index != 0 && index % attributeCount == 0)
                        {
                            sb.AppendLine();
                        }
                        var controlId = controlIds[index];
                        sb.Append($"{controlId} ");
                    }
                    
                    Debug.Log(sb.ToString());
                }
               
                // IDK why this is off-by-one
                int focusedIndex = Array.IndexOf(controlIds, EditorGUIUtility.keyboardControl + 1);
                
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
                            if (focusedIndex + attributeCount < controlIds.Length)
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
                        GUIUtility.keyboardControl = controlIds[nextFocusedIndex] - 1;
                        ev.Use(); // make sure to consume event if we used it
                    }
                }
            }
        }

        private DataScheme ImportFromCSV()
        {
            string filePath = EditorUtility.OpenFilePanel("Import from CSV", "", "csv");

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("Import canceled, no file path provided.");
                return null;
            }

            var schemaName = Path.GetFileNameWithoutExtension(filePath);
            var importedSchema = new DataScheme(schemaName);
            var rows = File.ReadAllLines(filePath);
            
            var header = rows[0].Split(',');
            
            importedSchema.Attributes.AddRange(header.Select(h => new AttributeDefinition
            {
                AttributeName = h,
                DataType = DataType.String, // TODO: determine datatype, maybe scan entries or hint in header name, e.g header (type)
                DefaultValue = string.Empty,
            }));

            for (var rowIdx = 1; rowIdx < rows.Length; rowIdx++)
            {
                var row = rows[rowIdx];
                var entries = row.Split(',');
                Assert.AreEqual(header.Length, entries.Length, $"{schemaName}.{rowIdx}");

                var entry = new DataEntry();
                for (var colIdx = 0; colIdx < header.Length; colIdx++)
                {
                    var attributeName = header[colIdx];
                    
                    entry.EntryData[attributeName] = entries[colIdx];
                }
                
                importedSchema.Entries.Add(entry);
            }
            
            return importedSchema;
        }
        
        private void ExportToCSV(DataScheme schema)
        {
            string filePath = EditorUtility.SaveFilePanel("Save CSV", "", $"{schema.SchemeName}.csv", "csv");

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("Export canceled, no file path provided.");
                return;
            }

            StringBuilder csvContent = new StringBuilder();

            // Add headers
            int attributeCount = schema.Attributes.Count;
            for (int i = 0; i < attributeCount; i++)
            {
                var attribute = schema.Attributes[i];
                
                csvContent.Append(attribute.AttributeName);

                // fence posting
                if (i != attributeCount - 1)
                {
                    csvContent.Append(",");
                }
            }
            csvContent.AppendLine();

            // Add data rows
            foreach (var entry in schema.Entries)
            {
                Assert.AreEqual(attributeCount, entry.EntryData.Count, "Entry data count mismatch.");
                for (int i = 0; i < attributeCount; i++)
                {
                    csvContent.Append(entry.EntryData[schema.Attributes[i].AttributeName]);
                    
                    // fence posting
                    if (i != attributeCount - 1)
                    {
                        csvContent.Append(",");
                    }
                }
                csvContent.AppendLine();
            }

            // Write to file
            File.WriteAllText(filePath, csvContent.ToString());
            Debug.Log($"Data exported successfully to {filePath}");
        }

        private static readonly GUILayoutOption DEFAULT_ENTRY_WIDTH = GUILayout.Width(150);
        
        public static void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            r.height = thickness;
            r.y+=padding/2;
            r.x-=2;
            r.width +=6;
            EditorGUI.DrawRect(r, color);
        }
        
        private void DrawVerticalLine(float thickness = 2)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(thickness), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, Color.white);
        }

        public static bool AddButton(string text) => Button(text, "Toolbar Plus");

        public static bool Button(string text, string iconName) =>
            GUILayout.Button(new GUIContent(text, EditorGUIUtility.IconContent(iconName).image), GUILayout.ExpandWidth(false));
    }
}