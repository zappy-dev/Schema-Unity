using System;
using System.IO;
using System.Linq;
using Schema.Core.Data;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;
using static Schema.Unity.Editor.SchemaLayout;
using Logger = Schema.Core.Logger;
using System.Collections.Generic;

namespace Schema.Unity.Editor
{
    public partial class SchemaEditorWindow
    {
        // Add this field to store filter values for each attribute
        private Dictionary<string, string> attributeFilters = new Dictionary<string, string>();
        

        private string GetFilterPrefsKey(string schemeName) => $"SchemaEditorWindow.AttributeFilters.{schemeName}";

        private void SaveAttributeFilters(string schemeName)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(attributeFilters);
            EditorPrefs.SetString(GetFilterPrefsKey(schemeName), json);
        }

        private void LoadAttributeFilters(string schemeName)
        {
            var key = GetFilterPrefsKey(schemeName);
            if (EditorPrefs.HasKey(key))
            {
                var json = EditorPrefs.GetString(key);
                attributeFilters = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                                  ?? new Dictionary<string, string>();
            }
            else
            {
                attributeFilters = new Dictionary<string, string>();
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

            // Load filters for the current schema (if not already loaded)
            LoadAttributeFilters(scheme.SchemeName);
            
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
                
                // Render table header and filter row
                RenderTableHeader(attributeCount, scheme);

                var sortOrder = GetSortOrderForScheme(scheme);
                var entries = scheme.GetEntries(sortOrder);

                // Apply attribute filters
                if (attributeFilters.Count > 0)
                {
                    entries = entries.Where(entry =>
                    {
                        foreach (var filter in attributeFilters)
                        {
                            var attributeRes = scheme.GetAttribute(filter.Key);
                            if (!attributeRes.Try(out var attribute))
                                return false;
                            if (!entry.GetData(attribute).Try(out var value) || value == null)
                                return false;
                            var filterStr = filter.Value.ToLower();
                            if (string.IsNullOrEmpty(filterStr))
                                continue;
                            if (!value.ToString().ToLower().Contains(filterStr))
                                return false;
                        }
                        return true;
                    }).ToList();
                }

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
                                    Logger.LogDbgWarning($"Setting {attribute} data for {entry}");
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
                                    scheme.SetDataOnEntry(entry, attributeName, entryValue);
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
                                        ReferenceDropdown.Draw(null, currentValue, refDataType, 
                                            (newValue) =>
                                            {
                                                scheme.SetDataOnEntry(entry, attributeName, newValue);
                                            },
                                            refDropdownWidth, cellStyle.DropdownStyle);
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
                                                scheme.SetDataOnEntry(entry, attributeName, entryValue);
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
            using (new GUILayout.VerticalScope())
            {
                // Header row
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("#", RightAlignedLabelStyle, 
                        GUILayout.Width(SETTINGS_WIDTH),
                        GUILayout.ExpandWidth(false));
                    for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                    {
                        var attribute = scheme.GetAttribute(attributeIdx);
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
                        var attributeContent = new GUIContent(attributeLabel, attributeIcon, attribute.AttributeToolTip);
                        if (DropdownButton(attributeContent, attribute.ColumnWidth))
                        {
                            RenderAttributeColumnOptions(attributeIdx, scheme, attribute, attributeCount);
                        }
                    }
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
                            newAttributeName = string.Empty;
                        }
                    }
                }
                // Filter row
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("", RightAlignedLabelStyle, 
                        GUILayout.Width(SETTINGS_WIDTH),
                        GUILayout.ExpandWidth(false));
                    // GUILayout.Space(SETTINGS_WIDTH); // for the row number column
                    for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                    {
                        var attribute = scheme.GetAttribute(attributeIdx);
                        string filterValue = attributeFilters.TryGetValue(attribute.AttributeName, out var val) ? val : string.Empty;
                        string newFilterValue = GUILayout.TextField(filterValue, GUILayout.Width(attribute.ColumnWidth));
                        if (newFilterValue != filterValue)
                        {
                            if (string.IsNullOrWhiteSpace(newFilterValue))
                            {
                                attributeFilters.Remove(attribute.AttributeName);
                            }
                            else
                            {
                                attributeFilters[attribute.AttributeName] = newFilterValue;
                            }
                            SaveAttributeFilters(scheme.SchemeName);
                        }
                    }
                    GUILayout.Space(100); // for the add attribute field
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

            var canMoveLeft = attributeIdx == 0;
            var canMoveRight = attributeIdx == attributeCount - 1;
            // attribute column ordering options
            columnOptionsMenu.AddItem(new GUIContent("Move To Front"), isDisabled: canMoveLeft, () =>
            {
                scheme.MoveAttributeRank(attribute, 0);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move Left"), isDisabled: canMoveLeft, () =>
            {
                scheme.IncreaseAttributeRank(attribute);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move Right"), isDisabled: canMoveRight, () =>
            {
                scheme.DecreaseAttributeRank(attribute);
                SaveDataScheme(scheme, alsoSaveManifest: false);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move To Back"), isDisabled: canMoveRight, () =>
            {
                scheme.MoveAttributeRank(attribute, attributeCount - 1);
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
    }
}