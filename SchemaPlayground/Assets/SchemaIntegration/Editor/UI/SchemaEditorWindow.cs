using System;
using System.Linq;
using System.Text;
using Schema.Core;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

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
                var schemaNames = Schema.Core.Schema.AllSchemes.ToArray();

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
                            newSchemeName = string.Empty; // clear out new schema field name since it's unlikely someone wants to make a new schema with the same name
                        }
                    }
                }
                
                GUILayout.Space(10);
                
                // render import options
                if (EditorGUILayout.DropdownButton(new GUIContent("Import"), 
                        FocusType.Keyboard, GUILayout.ExpandWidth(false)))
                {
                    GenericMenu menu = new GenericMenu();

                    foreach (var storageFormat in StorageUtil.AllFormats)
                    {
                        menu.AddItem(new GUIContent(storageFormat.Extension.ToUpper()), false, () =>
                        {
                            if (storageFormat.TryImport(out var importedSchema))
                            {
                                AddSchema(importedSchema);
                            }
                        });
                    }
                        
                    menu.ShowAsContext();
                }

                GUILayout.EndScrollView();
            }
        }

        private void AddSchema(DataScheme newSchema)
        {
            bool overwriteExisting = false;
            if (Core.Schema.DataSchemes.ContainsKey(newSchema.SchemaName))
            {
                overwriteExisting = EditorUtility.DisplayDialog("Add Schema", $"A Schema named {newSchema.SchemaName} already exists. " +
                                                                              $"Do you want to overwrite this sceham>", "Yes", "No");
            }
            
            latestResponse = Schema.Core.Schema.AddSchema(newSchema, overwriteExisting);
            switch (latestResponse.Status)
            {
                case RequestStatus.Error:
                    Debug.LogError(latestResponse.Payload);
                    break;
                case RequestStatus.Success:
                    Debug.Log($"New scheme '{newSchema.SchemaName}' created.");
                    OnSelectSchema(newSchema.SchemaName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnSelectSchema(string schemaName)
        {
            Debug.Log($"Opening {schemaName} for editing...");
            selectedSchemaName = schemaName;
            var schemaNames = Schema.Core.Schema.AllSchemes.ToArray();
            selectedSchemaIndex = Array.IndexOf(schemaNames, selectedSchemaName);
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
                GUILayout.Label($"Table View - {schema.SchemaName}", EditorStyles.boldLabel);

                using (new GUILayout.HorizontalScope())
                {
                    // render export options
                    if (EditorGUILayout.DropdownButton(new GUIContent("Export"), 
                            FocusType.Keyboard, GUILayout.ExpandWidth(false)))
                    {
                        GenericMenu menu = new GenericMenu();

                        foreach (var storageFormat in StorageUtil.AllFormats)
                        {
                            menu.AddItem(new GUIContent(storageFormat.Extension.ToUpper()), false, () =>
                            {
                                storageFormat.Export(schema);
                            });
                        }
                        
                        menu.ShowAsContext();
                    }
                }
                    
                tableViewScrollPosition = GUILayout.BeginScrollView(tableViewScrollPosition, alwaysShowHorizontal: true, alwaysShowVertical: true);
                
                // render columns
                using (new GUILayout.HorizontalScope())
                {
                    for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                    {
                        var attribute = schema.Attributes[attributeIdx];
                        GUILayout.Label(attribute.AttributeName, EditorStyles.boldLabel, GUILayout.MaxWidth(90));
                        
                        if (EditorGUILayout.DropdownButton(new GUIContent("", EditorGUIUtility.IconContent(EditorIcon.GEAR_ICON_NAME).image),
                                FocusType.Keyboard, GUILayout.MaxWidth(60)))
                        {
                            GenericMenu menu = new GenericMenu();

                            // attribute column ordering options
                            var moveLeftOption = new GUIContent("Move Left");
                            if (attributeIdx == 0)
                            {
                                menu.AddDisabledItem(moveLeftOption);
                            }
                            else
                            {
                                menu.AddItem(moveLeftOption, false, () =>
                                {
                                    schema.IncreaseAttributeRank(attribute);
                                });
                            }

                            var moveRightOption = new GUIContent("Move Right");
                            if (attributeIdx == attributeCount - 1)
                            {
                                menu.AddDisabledItem(moveRightOption);
                            }
                            else
                            {
                                menu.AddItem(moveRightOption, false, () =>
                                {
                                    schema.DecreaseAttributeRank(attribute);
                                });
                            }
                            
                            menu.AddSeparator("");
                            
                            // options to convert type
                            foreach (var builtInType in DataType.BuiltInTypes)
                            {
                                menu.AddItem(new GUIContent($"Convert Type/{builtInType.TypeName}"), builtInType.TypeName == attribute.DataType.TypeName,
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

                    // add new attribute form
                    newAttributeName = GUILayout.TextField(newAttributeName, GUILayout.ExpandWidth(false), GUILayout.MinWidth(100));
                    using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newAttributeName)))
                    {
                        if (AddButton("Add Attribute"))
                        {
                            Debug.Log($"Added new attribute to '{schema.SchemaName}'.`");
                            latestResponse = schema.AddAttribute(newAttributeName, DataType.String, string.Empty);
                            newAttributeName = string.Empty; // clear out attribute name field since it's unlikely someone wants to make another attribute with the same name
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
                                controlIds[entryIdx * attributeCount + attributeIdx] = lastControlId;

                                if (changed.changed)
                                {
                                    entry.EntryData[attributeName] = entryValue;
                                }
                            }
                        }
                    }
                }

                // add new entry form
                using (new EditorGUI.DisabledScope(schema.Attributes.Count == 0))
                {
                    if (AddButton("Add Entry"))
                    {
                        schema.CreateNewEntry();
                        Debug.Log($"Added entry to '{schema.SchemaName}'.`");
                    }
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

        public static bool AddButton(string text) => Button(text, EditorIcon.PLUS_ICON_NAME);

        public static bool Button(string text, string iconName) =>
            GUILayout.Button(new GUIContent(text, EditorGUIUtility.IconContent(iconName).image), GUILayout.ExpandWidth(false));
    }
}