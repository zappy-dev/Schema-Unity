using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Schema.Core.Data;
using Schema.Core.DataStructures;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;
using static Schema.Core.Logging.Logger;
using static Schema.Unity.Editor.SchemaLayout;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        #region Constants
        
        private static readonly ProfilerMarker _tableHeaderMarker = new ProfilerMarker("Schema.Unity.TableView.TableHeader");
        private static readonly ProfilerMarker _tableBodyMarker = new ProfilerMarker("Schema.Unity.TableView.TableBody");
        private static readonly ProfilerMarker _tableBodyFilterEntriesMarker = new ProfilerMarker("Schema.Unity.TableView.TableBody.FilterEntries");
        private static readonly ProfilerMarker _dataConvertMarker = new ProfilerMarker("Schema.Unity.TableView.ConvertData");
        private static readonly ProfilerMarker _dataCellMarker = new ProfilerMarker("Schema.Unity.TableView.DataCell");

        #endregion

        #region Fields and Properties
        
        private Vector2 tableViewBodyVerticalScrollPosition;
        private Vector2 tableViewHeaderHorizontalScrollPosition;

        #endregion
        
        
        #region Table Layout Infrastructure
        
        /// <summary>
        /// Handles table layout calculations for rect-based rendering
        /// </summary>
        private class TableLayout
        {
            public float RowHeight { get; set; } = 20f;
            public float HeaderHeight { get; set; } = 40f;
            public float SettingsWidth { get; set; } = SETTINGS_WIDTH;
            public Dictionary<int, float> ColumnWidths { get; set; } = new Dictionary<int, float>();
            
            public Rect GetRowRect(int rowIndex, float yOffset)
            {
                return new Rect(0, yOffset, Screen.width, RowHeight);
            }
            
            public Rect GetCellRect(int rowIndex, int columnIndex, float yOffset, DataScheme scheme)
            {
                float x = 0;
                float width = 0;
                
                if (columnIndex == -1) // Row number column
                {
                    x = 0;
                    width = SettingsWidth;
                }
                else
                {
                    x = SettingsWidth;
                    for (int i = 0; i < columnIndex; i++)
                    {
                        var iAttr = scheme.GetAttribute(i);
                        x += iAttr.ColumnWidth;
                    }
                    var attribute = scheme.GetAttribute(columnIndex);
                    width = attribute.ColumnWidth;
                }
                
                return new Rect(x, yOffset, width, RowHeight);
            }
            
            public Rect GetHeaderRect(float yOffset)
            {
                return new Rect(0, yOffset, Screen.width, HeaderHeight);
            }
        }
        
        private readonly TableLayout _tableLayout = new TableLayout();

        [NonSerialized] private string selectedSchemeLoadPath = null;

        #endregion

        private List<DataEntry> allEntries;
        private void RefreshTableEntriesForSelectedScheme()
        {
            if (string.IsNullOrEmpty(selectedSchemeName) ||
                !GetScheme(selectedSchemeName).Try(out var scheme))
            {
                allEntries = Enumerable.Empty<DataEntry>().ToList();
                return;
            }
            
            var sortOrder = GetSortOrderForScheme(scheme);
            var realAllEntries = scheme.GetEntries(sortOrder);
            
            var compiledFilters = GetAttributeFiltersForScheme(scheme);
            if (compiledFilters.Count <= 0)
            {
                allEntries = realAllEntries.ToList();
            }
            else
            {
                allEntries = realAllEntries.Where(entry =>
                {
                    // ensure low GC
                    foreach (var filter in compiledFilters)
                    {
                        var (attribute, needle) = filter;
                        var value = entry.GetDataDirect(attribute);
                        if (value == null)
                            return false;
                        // expensive to compare everything to strings
                        // make faster for numeric comparisons?
                        if (value is string s)
                        {
                            if (s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            var str = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                            if (string.IsNullOrEmpty(str) ||
                                str.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }).ToList();
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
                EditorGUILayout.HelpBox("Schema does not exist.", MessageType.Warning);
                return;
            }
            
            // Load core data
            var sortOrder = GetSortOrderForScheme(scheme);
            // allEntries = scheme.GetEntries(sortOrder).ToList();
            
            // Filters are loaded on scheme selection; avoid reloading during render
            
            int attributeCount = scheme.AttributeCount;
            int availableEntryCount = allEntries.Count;
            int totalEntryCount = scheme.EntryCount;
                
            // mapping entries to control IDs for focus/navigation management
            // For virtual scrolling, we only need control IDs for visible entries
            int visibleEntryCount = _virtualTableView.IsVirtualScrollingActive ? 
                Math.Min(totalEntryCount, 50) : totalEntryCount; // Limit to reasonable number for virtual scrolling
            int[] tableCellControlIds = new int[attributeCount * visibleEntryCount];

            using (new GUILayout.VerticalScope())
            {
                
                GUILayout.Label(
                    $"Table View - {scheme.SchemeName} - {availableEntryCount}/{totalEntryCount} {(totalEntryCount == 1 ? "entry" : "entries")}",
                    EditorStyles.boldLabel);

                if (showDebugView)
                {
                    GUILayout.Label($"{RuntimeHelpers.GetHashCode(scheme)}");
                }

                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Storage Path", DoNotExpandWidthOptions);
                    using (new EditorGUI.DisabledScope())
                    {
                        if (string.IsNullOrEmpty(selectedSchemeLoadPath) && GetManifestEntryForScheme(scheme).Try(out var schemeManifestEntry))
                        {
                            selectedSchemeLoadPath = schemeManifestEntry.FilePath;
                            selectedSchemeLoadPath = PathUtility.MakeAbsolutePath(selectedSchemeLoadPath, ContentLoadPath);
                        }

                        EditorGUILayout.TextField(selectedSchemeLoadPath);
                    }

                    var saveButtonText = scheme.IsDirty ? "Save*" : "Save";

                    if (GUILayout.Button(saveButtonText, ExpandWidthOptions))
                    {
                        LatestResponse = SaveDataScheme(scheme, alsoSaveManifest: false);
                    }

                    if (GUILayout.Button("Open", ExpandWidthOptions))
                    {
                        EditorUtility.RevealInFinder(selectedSchemeLoadPath);
                    }

                    // TODO: Open raw schema files
                    // if (GUILayout.Button("Open", ExpandWidthOptions) &&
                    //     GetManifestEntryForScheme(scheme).Try(out var manifestEntry))
                    // {
                    //     var storagePath = manifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
                    //     Application.OpenURL(storagePath);
                    // }

                    // render export options
                    if (EditorGUILayout.DropdownButton(new GUIContent("Export"),
                            FocusType.Keyboard, DoNotExpandWidthOptions))
                    {
                        GenericMenu menu = new GenericMenu();

                        foreach (var storageFormat in Storage.AllFormats)
                        {
                            menu.AddItem(new GUIContent(storageFormat.Extension.ToUpper()), false,
                                () => { storageFormat.Export(scheme); });
                        }

                        menu.ShowAsContext();
                    }
                }

                using (var tableScrollView = new EditorGUILayout.ScrollViewScope(tableViewHeaderHorizontalScrollPosition,
                           alwaysShowHorizontal: true,
                           alwaysShowVertical: false))
                {
                    tableViewHeaderHorizontalScrollPosition = tableScrollView.scrollPosition;
                    
                    // Freeze table header to top of the viewport
                    RenderTableHeader(attributeCount, scheme);
                    
                    using (var tableBodyScrollView = new EditorGUILayout.ScrollViewScope(tableViewBodyVerticalScrollPosition,
                               alwaysShowHorizontal: false,
                               alwaysShowVertical: true))
                    {
                        // Update scroll position from the scope
                        if (tableBodyScrollView.scrollPosition != tableViewBodyVerticalScrollPosition) 
                        {
                            tableViewBodyVerticalScrollPosition = tableBodyScrollView.scrollPosition;
                            GUI.FocusControl(null);
                        }

                        LogDbgVerbose(
                            $"TableView: Will pass to VirtualTableView - viewportHeight={lastScrollViewRect.height:F1}");

                        _tableBodyMarker.Begin();

                        // Calculate visible range for virtual scrolling
                        var visibleRange = _virtualTableView.CalculateVisibleRange(
                            tableViewBodyVerticalScrollPosition,
                            lastScrollViewRect,
                            allEntries.Count,
                            scheme.SchemeName,
                            sortOrder,
                            attributeFilters);

                        // Debug logging to track visible range issues
                        if (visibleRange.end > allEntries.Count)
                        {
                            LogDbgWarning(
                                $"Visible range end ({visibleRange.end}) exceeds total entries ({allEntries.Count}). Clamping to valid range.");
                            visibleRange = (visibleRange.start, Math.Min(visibleRange.end, allEntries.Count));
                        }

                        // Debug logging for rendering
                        LogDbgVerbose(
                            $"TableView: Rendering range {visibleRange.start}-{visibleRange.end}, total entries {allEntries.Count}");

                        // Efficient approach: use single spacers for all invisible rows instead of individual spaces
                        // This reduces GUI allocations and improves performance
                        int renderedCount = 0;

                        // Top spacer for rows before visible range
                        if (visibleRange.start > 0)
                        {
                            float topSpacerHeight = visibleRange.start * _tableLayout.RowHeight;
                            GUILayout.Space(topSpacerHeight);
                            LogDbgVerbose(
                                $"TableView: Added top spacer of {topSpacerHeight:F1} pixels for {visibleRange.start} invisible rows");
                        }

                        // Render visible rows
                        for (int i = visibleRange.start; i < visibleRange.end; i++)
                        {
                            var rowRect = GUILayoutUtility.GetRect(0, _tableLayout.RowHeight);
                            RenderTableRow(rowRect, allEntries.ElementAt(i), i, attributeCount, scheme,
                                tableCellControlIds, visibleEntryCount, visibleRange.start);
                            renderedCount++;
                            LogDbgVerbose($"TableView: Rendered row {i} at position {rowRect.y:F1}");
                        }

                        // Bottom spacer for rows after visible range
                        if (visibleRange.end < allEntries.Count)
                        {
                            float bottomSpacerHeight = (allEntries.Count - visibleRange.end) * _tableLayout.RowHeight;
                            GUILayout.Space(bottomSpacerHeight);
                            LogDbgVerbose(
                                $"TableView: Added bottom spacer of {bottomSpacerHeight:F1} pixels for {allEntries.Count - visibleRange.end} invisible rows");
                        }

                        LogDbgVerbose(
                            $"TableView: Rendered {renderedCount} visible rows out of {visibleRange.end - visibleRange.start} expected");

                        // Update the cell count in VirtualTableView for debug display
                        int totalCellsDrawn = renderedCount * attributeCount; // Each row has attributeCount cells
                        _virtualTableView.UpdateCellCount(totalCellsDrawn);
                        LogDbgVerbose(
                            $"TableView: Total cells drawn: {totalCellsDrawn} ({renderedCount} rows × {attributeCount} attributes)");

                        // GUILayout.EndScrollView();
                        _tableBodyMarker.End();
                    }
                }

                if (Event.current.type == EventType.Repaint)
                {
                    lastScrollViewRect = GUILayoutUtility.GetLastRect();
                }
                
                // add new entry form
                if (scheme.AttributeCount > 0)
                {
                    if (AddButton("Create New Entry", expandWidth: true, height: 50f))
                    {
                        scheme.CreateNewEmptyEntry();
                        LogDbgVerbose($"Added entry to '{scheme.SchemeName}'.");
                        OnSelectedSchemeChanged?.Invoke();
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
                
                // For virtual scrolling, we need to convert local index back to global index
                if (_virtualTableView.IsVirtualScrollingActive && focusedIndex != -1)
                {
                    int localEntryIdx = focusedIndex / attributeCount;
                    int localAttributeIdx = focusedIndex % attributeCount;
                    focusedIndex = (_virtualTableView.VisibleRange.start + localEntryIdx) * attributeCount + localAttributeIdx;
                }
                
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
                            if (focusedIndex + attributeCount < allEntries.Count * attributeCount)
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
                            if (focusedIndex + 1 < allEntries.Count * attributeCount)
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
                        // For virtual scrolling, we need to convert global index to local index
                        if (_virtualTableView.IsVirtualScrollingActive)
                        {
                            int globalEntryIdx = nextFocusedIndex / attributeCount;
                            int globalAttributeIdx = nextFocusedIndex % attributeCount;
                            
                            // Check if the target entry is in the visible range
                            if (globalEntryIdx >= _virtualTableView.VisibleRange.start && globalEntryIdx < _virtualTableView.VisibleRange.end)
                            {
                                int localEntryIdx = globalEntryIdx - _virtualTableView.VisibleRange.start;
                                int localIndex = localEntryIdx * attributeCount + globalAttributeIdx;
                                if (localIndex >= 0 && localIndex < tableCellControlIds.Length)
                                {
                                    GUIUtility.keyboardControl = tableCellControlIds[localIndex] - 1;
                                    ev.Use();
                                }
                            }
                            else
                            {
                                // Target is outside visible range, scroll to it
                                float targetY = globalEntryIdx * _virtualTableView.TotalContentHeight / allEntries.Count;
                                tableViewBodyVerticalScrollPosition.y = targetY;
                                ev.Use();
                            }
                        }
                        else
                        {
                            // IDK why this is off-by-one
                            GUIUtility.keyboardControl = tableCellControlIds[nextFocusedIndex] - 1;
                            ev.Use(); // make sure to consume event if we used it
                        }
                    }
                }
            }
        }

        private void RenderTableHeader(int attributeCount, DataScheme scheme)
        {
            using (_tableHeaderMarker.Auto())
            {
                using (new GUILayout.VerticalScope())
                {
                    // Header row
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
                        newAttributeName = GUILayout.TextField(newAttributeName, GUILayout.MinWidth(100),
                            GUILayout.ExpandWidth(false));
                        using (new EditorGUI.DisabledScope(disabled: string.IsNullOrEmpty(newAttributeName)))
                        {
                            if (AddButton("Add Attribute"))
                            {
                                LogDbgVerbose($"Added new attribute to '{scheme.SchemeName}'.`");
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

                                newAttributeName =
                                    string.Empty; // clear out attribute name field since it's unlikely someone wants to make another attribute with the same name
                            }
                        }
                    }

                    // Filter row
                    using (new GUILayout.HorizontalScope())
                    {
                        // Filters are already in-memory; avoid reloading here
                        EditorGUILayout.LabelField("", RightAlignedLabelStyle,
                            GUILayout.Width(SETTINGS_WIDTH),
                            GUILayout.ExpandWidth(false));
                        // GUILayout.Space(SETTINGS_WIDTH); // for the row number column
                        for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                        {
                            var attribute = scheme.GetAttribute(attributeIdx);
                            string filterValue = attributeFilters.TryGetValue(attribute.AttributeName, out var val)
                                ? val
                                : string.Empty;

                            switch (attribute.DataType) 
                            {
                                case ReferenceDataType refDataType:
                                    if (GUILayout.Button(filterValue, GUILayout.Width(attribute.ColumnWidth)))
                                    {
                                        var referenceEntryOptions = new GenericMenu();
                
                                        if (GetScheme(refDataType.ReferenceSchemeName).Try(out var refSchema))
                                        {
                                            foreach (var identifierValue in refSchema.GetIdentifierValues())
                                            {
                                                var optionValue = identifierValue.ToString();
                                                referenceEntryOptions.AddItem(
                                                    new GUIContent(optionValue),
                                                    on: filterValue.Contains(optionValue),
                                                    () =>
                                                    {
                                                        string newFilterValue;
                                                        if (filterValue.Contains(optionValue))
                                                        {
                                                            newFilterValue = filterValue.Replace(optionValue, "");
                                                        }
                                                        else
                                                        {
                                                            newFilterValue = (string.IsNullOrEmpty(filterValue)) ? optionValue : $"{filterValue},{optionValue}";
                                                        }
                                                        newFilterValue = newFilterValue.Replace(",,", "");
                                                        UpdateAttributeFilter(attribute.AttributeName, newFilterValue);
                                                    });
                                            }
                                        }
                
                                        referenceEntryOptions.ShowAsContext();
                                    }
                                    break;
                                
                                default:
                                    string newFilterValue = GUILayout.TextField(filterValue, GUILayout.Width(attribute.ColumnWidth));
                            
                                    // On attribute filter change
                                    if (newFilterValue != filterValue)
                                    {
                                        UpdateAttributeFilter(attribute.AttributeName, newFilterValue);
                                    }
                                    break;
                            }
                        }

                        GUILayout.Space(100); // for the add attribute field
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
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Up"), isDisabled: canMoveUp, () =>
            {
                scheme.MoveUpEntry(entry);
                SaveDataScheme(scheme, alsoSaveManifest: false);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Down"), isDisabled: canMoveDown, () =>
            {
                scheme.MoveDownEntry(entry);
                SaveDataScheme(scheme, alsoSaveManifest: false);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddItem(new GUIContent("Move To Bottom"), isDisabled: canMoveDown, () =>
            {
                scheme.MoveEntry(entry, entryCount - 1);
                SaveDataScheme(scheme, alsoSaveManifest: false);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddSeparator("");
            rowOptionsMenu.AddItem(new GUIContent("Delete Entry"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Schema", "Are you sure you want to delete this entry?", "Yes, delete this entry", "No, cancel"))
                {
                    LatestResponse = scheme.DeleteEntry(entry);
                    SaveDataScheme(scheme, alsoSaveManifest: false);
                    OnSelectedSchemeChanged?.Invoke();
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
        
        #region Rect-Based Cell Rendering Methods
        
        /// <summary>
        /// Renders a row background with the specified cell style
        /// </summary>
        private void RenderRowBackground(Rect rowRect, CellStyle cellStyle)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = cellStyle.BackgroundColor;
            GUI.DrawTexture(rowRect, Texture2D.grayTexture); // TODO: Replace with a nice texture for rows?
            GUI.backgroundColor = originalColor;
        }
        
        /// <summary>
        /// Renders a row number cell
        /// </summary>
        private void RenderRowNumberCell(Rect cellRect, int rowNumber, CellStyle cellStyle, int entryIdx, int entryCount, DataScheme scheme, DataEntry entry)
        {
            if (GUI.Button(cellRect, rowNumber.ToString(), cellStyle.DropdownStyle))
            {
                RenderEntryRowOptions(entryIdx, entryCount, scheme, entry);
            }
        }
        
        /// <summary>
        /// Renders a data cell based on the attribute data type
        /// </summary>
        private void RenderDataCell(Rect cellRect, DataEntry entry, AttributeDefinition attribute, object entryValue, CellStyle cellStyle, int entryIdx, int attributeIdx, DataScheme scheme, int[] tableCellControlIds, int visibleEntryCount, int visibleRangeStart)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = cellStyle.BackgroundColor;
            
            // For virtual scrolling, we need to map the global entry index to local visible index
            int localEntryIdx = _virtualTableView.IsVirtualScrollingActive
                ? entryIdx - visibleRangeStart
                : entryIdx;
            
            if (localEntryIdx >= 0 && localEntryIdx < visibleEntryCount)
            {
                tableCellControlIds[localEntryIdx * scheme.AttributeCount + attributeIdx] = GUIUtility.GetControlID(FocusType.Passive);
            }
            
            // Render based on data type
            switch (attribute.DataType)
            {
                case IntegerDataType _:
                    RenderIntegerCell(cellRect, Convert.ToInt32(entryValue), cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case FloatingPointDataType _:
                    RenderFloatCell(cellRect, Convert.ToSingle(entryValue), cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case BooleanDataType _:
                    bool boolValue = false;
                    if (entryValue is bool b)
                        boolValue = b;
                    else if (entryValue is string s && bool.TryParse(s, out var parsed))
                        boolValue = parsed;
                    else if (entryValue is int i)
                        boolValue = i != 0;
                    RenderBooleanCell(cellRect, boolValue, cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case FilePathDataType _:
                    RenderFilePathCell(cellRect, entryValue.ToString(), cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case ReferenceDataType refDataType:
                    RenderReferenceCell(cellRect, entryValue, refDataType, cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case DateTimeDataType _:
                    RenderDateTimeCell(cellRect, entryValue is DateTime dt ? dt : DateTime.Now, cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case TextDataType _:
                    RenderTextFieldCell(cellRect, entryValue, cellStyle, value => 
                        UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                case GuidDataType _:
                    RenderGuidCell(cellRect, entryValue is Guid ? (Guid)entryValue : default, cellStyle,
                        value => UpdateEntryValue(entry, attribute, value, scheme));
                    break;
                default:
                    RenderUnmappedFieldCell(cellRect, entryValue, cellStyle);
                    break;
            }
            
            GUI.backgroundColor = originalColor;
        }

        private void RenderGuidCell(Rect cellRect, Guid entryValue, CellStyle cellStyle, Action<object> action)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(entryValue.ToString().Replace("-", string.Empty));
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset != null)
            {
                var assetType = asset.GetType();
                EditorGUI.ObjectField(cellRect, asset, assetType);
            }
            else
            {
                EditorGUI.TextField(cellRect, entryValue.ToString(), cellStyle.FieldStyle);
            }
        }

        /// <summary>
        /// Renders an integer field cell
        /// </summary>
        private void RenderIntegerCell(Rect cellRect, int value, CellStyle cellStyle, Action<int> onValueChanged)
        {
            // var newValue = EditorGUI.IntField(cellRect, value, cellStyle.FieldStyle);
            var newValue = SchemaGUI.IntField(cellRect, value, cellStyle.FieldStyle);
            if (newValue != value)
            {
                onValueChanged?.Invoke(newValue);
            }
        }
        
        /// <summary>
        /// Renders an floating-point field cell
        /// </summary>
        private void RenderFloatCell(Rect cellRect, float value, CellStyle cellStyle, Action<float> onValueChanged)
        {
            // var newValue = EditorGUI.IntField(cellRect, value, cellStyle.FieldStyle);
            var newValue = SchemaGUI.FloatField(cellRect, value, cellStyle.FieldStyle);
            if (newValue != value)
            {
                onValueChanged?.Invoke(newValue);
            }
        }
        
        /// <summary>
        /// Renders a boolean toggle cell
        /// </summary>
        private void RenderBooleanCell(Rect cellRect, bool value, CellStyle cellStyle, Action<bool> onValueChanged)
        {
            var newValue = EditorGUI.Toggle(cellRect, value);
            if (newValue != value)
            {
                onValueChanged?.Invoke(newValue);
            }
        }
        
        /// <summary>
        /// Renders a text field cell
        /// </summary>
        private void RenderTextFieldCell(Rect cellRect, object value, CellStyle cellStyle, Action<object> onValueChanged)
        {
            var stringValue = value?.ToString() ?? string.Empty;
            var newValue = EditorGUI.TextField(cellRect, stringValue, cellStyle.FieldStyle);
            if (newValue != stringValue)
            {
                onValueChanged?.Invoke(newValue);
            }
        }
        
        /// <summary>
        /// Renders a unmapped field cell
        /// </summary>
        private void RenderUnmappedFieldCell(Rect cellRect, object value, CellStyle cellStyle)
        {
            var stringValue = value?.ToString() ?? string.Empty;
            EditorGUI.TextField(cellRect, stringValue, cellStyle.FieldStyle);
        }
        
        /// <summary>
        /// Renders a file path cell
        /// </summary>
        private void RenderFilePathCell(Rect cellRect, string filePath, CellStyle cellStyle, Action<string> onValueChanged)
        {
            var filePreview = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(filePreview))
            {
                filePreview = "...";
            }
            
            var fileContent = new GUIContent(filePreview, tooltip: filePath);
            if (GUI.Button(cellRect, fileContent, cellStyle.ButtonStyle))
            {
                var selectedFilePath = EditorUtility.OpenFilePanel("Schema - File Selection", "", "");
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    onValueChanged?.Invoke(selectedFilePath);
                }
            }
        }
        
        /// <summary>
        /// Renders a reference data type cell
        /// </summary>
        private void RenderReferenceCell(Rect cellRect, object value, ReferenceDataType refDataType, CellStyle cellStyle, Action<object> onValueChanged)
        {
            var gotoButtonWidth = 20f;
            var refDropdownWidth = cellRect.width - gotoButtonWidth;
            var currentValue = value?.ToString() ?? "...";
            
            var dropdownRect = new Rect(cellRect.x, cellRect.y, refDropdownWidth, cellRect.height);
            var gotoRect = new Rect(cellRect.x + refDropdownWidth, cellRect.y, gotoButtonWidth, cellRect.height);
            
            if (GUI.Button(dropdownRect, currentValue, cellStyle.DropdownStyle))
            {
                var referenceEntryOptions = new GenericMenu();
                
                if (GetScheme(refDataType.ReferenceSchemeName).Try(out var refSchema))
                {
                    foreach (var identifierValue in refSchema.GetIdentifierValues())
                    {
                        referenceEntryOptions.AddItem(
                            new GUIContent(identifierValue.ToString()),
                            on: identifierValue.Equals(currentValue),
                            () => onValueChanged?.Invoke(identifierValue));
                    }
                }
                
                referenceEntryOptions.ShowAsContext();
            }
            
            if (GUI.Button(gotoRect, "O"))
            {
                FocusOnEntry(refDataType.ReferenceSchemeName, refDataType.ReferenceAttributeName, currentValue);
                GUI.FocusControl(null);
            }
        }
        
        private static readonly LRUCache<DateTime, string> DateTimeCache = new LRUCache<DateTime, string>(1024);
        
        /// <summary>
        /// Renders a date time cell
        /// </summary>
        private void RenderDateTimeCell(Rect cellRect, DateTime value, CellStyle cellStyle, Action<DateTime> onValueChanged)
        {
            const string dateTimeFormat = "yyyy-MM-dd HH:mm:ss";
            if (!DateTimeCache.TryGet(value, out var dateTimeString))
            {
                dateTimeString = value.ToString(dateTimeFormat);
                DateTimeCache.Put(value, dateTimeString);
            }
            var result = EditorGUI.TextField(cellRect, dateTimeString, cellStyle.FieldStyle);
            
            if (result != dateTimeString && DateTime.TryParse(result, out DateTime parsedDateTime))
            {
                onValueChanged?.Invoke(parsedDateTime);
            }
        }
        
        /// <summary>
        /// Updates an entry value and triggers the appropriate async operations
        /// </summary>
        private void UpdateEntryValue(DataEntry entry, AttributeDefinition attribute, object newValue, DataScheme scheme)
        {
            var attributeName = attribute.AttributeName;
            
            if (attribute.IsIdentifier)
            {
                if (scheme.GetIdentifierValues().Any(otherAttributeName => otherAttributeName.Equals(newValue)))
                {
                    EditorUtility.DisplayDialog("Schema", $"Attribute '{attribute.AttributeName}' with value '{newValue}' already exists.", "OK");
                    return;
                }
                
                UpdateIdentifierValue(scheme.SchemeName, attributeName, entry.GetData(attributeName), newValue);
            }
            else
            {
                _ = ExecuteSetDataOnEntryAsync(scheme, entry, attributeName, newValue);
            }
            
            LatestResponse = Save();
        }
        
        /// <summary>
        /// Renders a single table row using rect-based rendering
        /// </summary>
        private void RenderTableRow(Rect rowRect, DataEntry entry, int entryIdx, int attributeCount, DataScheme scheme, int[] tableCellControlIds, int visibleEntryCount, int visibleRangeStart)
        {
            var cellStyle = GetRowCellStyle(entryIdx);
            
            // Render row background
            RenderRowBackground(rowRect, cellStyle);
            
            // Calculate cell positions relative to the row rect
            float currentX = rowRect.x;
            
            // Render row number cell
            var rowNumberRect = new Rect(currentX, rowRect.y, _tableLayout.SettingsWidth, _tableLayout.RowHeight);
            RenderRowNumberCell(rowNumberRect, entryIdx + 1, cellStyle, entryIdx, scheme.EntryCount, scheme, entry);
            currentX += _tableLayout.SettingsWidth;
            
            // Render data cells
            for (int attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
            {
                var attribute = scheme.GetAttribute(attributeIdx);
                var cellRect = new Rect(currentX, rowRect.y, attribute.ColumnWidth, _tableLayout.RowHeight);
                var entryValue = GetEntryValue(scheme, entry, attribute);
                
                using (_dataCellMarker.Auto()) 
                {
                    RenderDataCell(cellRect, entry, attribute, entryValue, cellStyle, entryIdx, attributeIdx, scheme, tableCellControlIds, visibleEntryCount, visibleRangeStart);
                }
                
                currentX += attribute.ColumnWidth;
            }
        }
        
        /// <summary>
        /// Gets the entry value for an attribute, handling null values and data conversion
        /// </summary>
        private object GetEntryValue(DataScheme scheme, DataEntry entry, AttributeDefinition attribute)
        {
            object entryValue = entry.GetDataDirect(attribute);
            
            if (entryValue == null)
            {
                LogDbgWarning($"Setting {attribute} data for {entry}");
                entryValue = attribute.CloneDefaultValue();
                entry.SetData(attribute.AttributeName, entryValue);
            }
            else if (attribute.CheckIfValidData(entryValue).Failed)
            {
                using (_dataConvertMarker.Auto())
                {
                    if (DataType.ConvertData(entryValue, DataType.Default, attribute.DataType, attribute.Context).Try(out var convertedValue))
                    {
                        entryValue = convertedValue;
                        entry.SetData(attribute.AttributeName, entryValue);
                    }
                    else
                    {
                        entryValue = attribute.CloneDefaultValue();
                        entry.SetData(attribute.AttributeName, entryValue);
                    }
                }
            }
            
            return entryValue;
        }
        
        #endregion
    }
}