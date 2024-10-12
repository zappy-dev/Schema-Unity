using System;
using System.Linq;
using Schema.Core;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public class SchemeEditorWindow : EditorWindow
    {
        private string newSchemeName = "";
        private SchemaResponse latestResponse;
        private Vector2 explorerScrollPosition;
        private Vector2 tableViewScrollPosition;

        private string selectedSchemaName = string.Empty;
        private string newAttributeName = string.Empty;
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

        private void OnGUI()
        {
            GUILayout.Label("Scheme Editor", EditorStyles.boldLabel);

            if (latestResponse.Payload != null)
            {
                EditorGUILayout.HelpBox(latestResponse.Payload.ToString(), latestResponse.MessageType());
            }

            GUILayout.BeginHorizontal();
            // Scrollable area to list existing schemes

            OnSchemaExplorerGUI();

            DrawVerticalLine();

            OnTableViewGUI();
            // Table View
            GUILayout.EndHorizontal();
        }

        private void OnSchemaExplorerGUI()
        {
            // using var _ = new ColorScope(Color.cyan);
            
            using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false)))
            {
                GUILayout.Label($"Schema Explorer ({Schema.Core.Schema.DataSchemes.Count} count):", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                explorerScrollPosition = GUILayout.BeginScrollView(explorerScrollPosition, GUILayout.Width(200), GUILayout.ExpandWidth(false));

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
                            latestResponse = Schema.Core.Schema.CreateNewSchema(newSchemeName);
                            switch (latestResponse.Status)
                            {
                                case RequestStatus.Error:
                                    Debug.LogError(latestResponse.Payload);
                                    break;
                                case RequestStatus.Success:
                                    Debug.Log($"New scheme '{newSchemeName}' created.");
                                    OnSelectSchema(newSchemeName);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            // Logic to add the new scheme (e.g., creating a new JSON file).
                        }
                    }
                }

                GUILayout.EndScrollView();
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
                EditorGUILayout.HelpBox("Select a schema from the explorer to view in the table", MessageType.Info);
            }
            else
            {
                var schema = Schema.Core.Schema.DataSchemes[selectedSchemaName];
                using (new GUILayout.VerticalScope())
                {
                    GUILayout.Label($"Table View - {schema.SchemeName}", EditorStyles.boldLabel);
                    
                    tableViewScrollPosition = GUILayout.BeginScrollView(tableViewScrollPosition, alwaysShowHorizontal: true, alwaysShowVertical: true);

                    // render columns
                    using (new GUILayout.HorizontalScope())
                    {
                        foreach (var attribute in schema.Attributes)
                        {
                            GUILayout.Label(attribute.AttributeName, EditorStyles.boldLabel, DEFAULT_ENTRY_WIDTH);
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
                    foreach (var entry in schema.Entries)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            foreach (var attribute in schema.Attributes)
                            {
                                var attributeName = attribute.AttributeName;
                                var entryValue = entry.EntryData[attributeName];
                                using (var changed = new EditorGUI.ChangeCheckScope())
                                {
                                    entryValue = GUILayout.TextField(entryValue.ToString(), DEFAULT_ENTRY_WIDTH);

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

        public static bool AddButton(string text) =>
            GUILayout.Button(new GUIContent(text, EditorGUIUtility.IconContent("Toolbar Plus").image), GUILayout.ExpandWidth(false));
    }
}

