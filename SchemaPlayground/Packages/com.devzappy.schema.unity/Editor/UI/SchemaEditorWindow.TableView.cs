using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Schema.Core;
using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.DataStructures;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Runtime.Type;
using Schema.Unity.Editor.Ext;
using Unity.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static Schema.Core.Schema;
using static Schema.Core.Logging.Logger;
using static Schema.Unity.Editor.LayoutUtils;
using static Schema.Unity.Editor.SchemaLayout;
using Logger = Schema.Core.Logging.Logger;
using Object = UnityEngine.Object;

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
                        var iAttr = scheme.GetAttribute(i).Result;
                        x += iAttr.ColumnWidth;
                    }
                    var attribute = scheme.GetAttribute(columnIndex).Result;
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
        
        // Cache for row heights to avoid recalculating every frame
        private Dictionary<string, Dictionary<int, float>> _rowHeightCache = new Dictionary<string, Dictionary<int, float>>();
        private string _lastCachedSchemeName = null;
        
        /// <summary>
        /// Calculates the required height for a row based on its content
        /// </summary>
        private float CalculateRowHeight(DataEntry entry, DataScheme scheme)
        {
            const float baseRowHeight = 20f;
            const float itemHeight = 20f;
            const float headerHeight = 22f;
            const float spacing = 2f;
            const float minListHeight = 50f; // Minimum height for a list cell
            const float assetPreviewHeight = 64f; // Height for asset previews (e.g., Texture)
            
            float maxHeight = baseRowHeight;
            
            // Check each attribute for list data types and asset types
            for (int i = 0; i < scheme.AttributeCount; i++)
            {
                var attribute = scheme.GetAttribute(i).Result;
                if (attribute.DataType is ListDataType)
                {
                    var entryValue = entry.GetDataDirect(attribute);
                    int itemCount = 0;
                    
                    if (entryValue != null && entryValue is IEnumerable enumerable && !(entryValue is string))
                    {
                        foreach (var _ in enumerable)
                        {
                            itemCount++;
                        }
                    }
                    
                    // Calculate list height: header + (items * itemHeight) + spacing
                    float listHeight = headerHeight + (itemCount * (itemHeight + spacing)) + spacing * 2;
                    listHeight = Math.Max(listHeight, minListHeight);
                    maxHeight = Math.Max(maxHeight, listHeight);
                }
                else if (attribute.DataType is UnityAssetDataType unityAssetDataType)
                {
                    // For texture assets and other visual assets, use a taller height to show preview
                    if (typeof(Texture).IsAssignableFrom(unityAssetDataType.ObjectType) ||
                        typeof(Sprite).IsAssignableFrom(unityAssetDataType.ObjectType))
                    {
                        maxHeight = Math.Max(maxHeight, assetPreviewHeight);
                    }
                }
            }
            
            return maxHeight;
        }

        [NonSerialized] private string selectedSchemeLoadPath = null;

        #endregion

        private List<DataEntry> allEntries;
        private void RefreshTableEntriesForSelectedScheme()
        {
            var refreshCtx = new SchemaContext
            {
                Driver = "Editor_Refresh_Table_Entries"
            };
            
            if (string.IsNullOrEmpty(selectedSchemeName) ||
                !GetScheme(refreshCtx, selectedSchemeName).Try(out var scheme))
            {
                allEntries = Enumerable.Empty<DataEntry>().ToList();
                return;
            }
            
            Logger.LogDbgVerbose($"Refreshing Table Entries for scheme: {scheme}");
            var sortOrder = GetSortOrderForScheme(scheme);
            if (!scheme.GetEntries(sortOrder, context: refreshCtx)
                .Try(out var realAllEntries, out var entryError)) return;
            
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
        
        private void RenderTableView(SchemaContext renderCtx)
        {
            if (string.IsNullOrEmpty(selectedSchemeName))
            {
                EditorGUILayout.HelpBox("Choose a Schema from the Schema Explorer to view in the table", MessageType.Info);
                return;
            }

            if (!GetScheme(renderCtx, selectedSchemeName).Try(out var scheme))
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
            
            // Declare these at function scope so they're accessible by keyboard navigation code
            (int start, int end) visibleRange = (0, 0);
            int visibleEntryCount = 0;
            int[] tableCellControlIds = Array.Empty<int>();

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
                    string pathLabel = string.Empty;
#if SCHEMA_DEBUG
                    pathLabel = $"Storage Path ({RuntimeHelpers.GetHashCode(scheme)})";
#else
                    pathLabel = "Storage Path";
#endif
                    EditorGUILayout.LabelField(pathLabel, DoNotExpandWidthOptions);
                    using (new EditorGUI.DisabledScope())
                    {
                        if (string.IsNullOrEmpty(selectedSchemeLoadPath) && GetManifestEntryForScheme(scheme).Try(out var schemeManifestEntry))
                        {
                            selectedSchemeLoadPath = schemeManifestEntry.FilePath;
                            selectedSchemeLoadPath = PathUtility.MakeAbsolutePath(selectedSchemeLoadPath, ProjectPath);
                        }

                        EditorGUILayout.TextField(selectedSchemeLoadPath);
                    }

                    if (GUILayout.Button("Open", ExpandWidthOptions))
                    {
                        EditorUtility.RevealInFinder(selectedSchemeLoadPath);
                    }

                    var saveButtonText = scheme.IsDirty ? "Save*" : "Save";

                    if (GUILayout.Button(saveButtonText, ExpandWidthOptions))
                    {
                        LatestResponse = SaveDataScheme(new SchemaContext
                        {
                            Scheme = scheme,
                            Driver = "User_Save_Scheme",
                        }, scheme, alsoSaveManifest: false);
                    }

                    if (GUILayout.Button("Publish", ExpandWidthOptions))
                    {
                        BulkPublishSchemes(new SchemaContext
                        {
                            Scheme = scheme,
                            Driver = "User_Request_Publish_Scheme",
                        }, new [] { selectedSchemeName });
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
                        RenderExportOptions(scheme);
                    }
                }

                // TODO: Fix this double scrolling window
                using (var tableScrollView = new EditorGUILayout.ScrollViewScope(tableViewHeaderHorizontalScrollPosition,
                           alwaysShowHorizontal: true,
                           alwaysShowVertical: false))
                {
                    tableViewHeaderHorizontalScrollPosition = tableScrollView.scrollPosition;
                    
                    // Freeze table header to top of the viewport
                    RenderTableHeader(renderCtx, attributeCount, scheme);
                    
                    using (var tableBodyScrollView = new EditorGUILayout.ScrollViewScope(tableViewBodyVerticalScrollPosition,
                               alwaysShowHorizontal: false,
                               alwaysShowVertical: true))
                    {
                        // Update scroll position from the scope
                        if (tableBodyScrollView.scrollPosition != tableViewBodyVerticalScrollPosition) 
                        {
                            tableViewBodyVerticalScrollPosition = tableBodyScrollView.scrollPosition;
                            ReleaseControlFocus();
                        }

                        _tableBodyMarker.Begin();

                        // Calculate visible range for virtual scrolling
                        visibleRange = _virtualTableView.CalculateVisibleRange(
                            tableViewBodyVerticalScrollPosition,
                            lastScrollViewRect,
                            allEntries.Count,
                            scheme.SchemeName,
                            sortOrder,
                            attributeFilters);

                        // Debug logging to track visible range issues
                        if (visibleRange.end > allEntries.Count)
                        {
                            visibleRange = (visibleRange.start, Math.Min(visibleRange.end, allEntries.Count));
                        }
                        
                        // mapping entries to control IDs for focus/navigation management
                        // For virtual scrolling, we only need control IDs for visible entries
                        // Allocate based on actual visible range to prevent IndexOutOfRangeException
                        visibleEntryCount = visibleRange.end - visibleRange.start;
                        tableCellControlIds = new int[attributeCount * visibleEntryCount];

                        // Efficient approach: use single spacers for all invisible rows instead of individual spaces
                        // This reduces GUI allocations and improves performance
                        int renderedCount = 0;

                        // Calculate row heights for all entries (needed for proper scrolling)
                        // Use cache to avoid recalculating every frame
                        if (_lastCachedSchemeName != scheme.SchemeName || !_rowHeightCache.ContainsKey(scheme.SchemeName) || scheme.IsDirty)
                        {
                            _rowHeightCache[scheme.SchemeName] = new Dictionary<int, float>();
                            for (int i = 0; i < allEntries.Count; i++)
                            {
                                _rowHeightCache[scheme.SchemeName][i] = CalculateRowHeight(allEntries[i], scheme);
                            }
                            _lastCachedSchemeName = scheme.SchemeName;
                        }
                        
                        var rowHeights = _rowHeightCache[scheme.SchemeName];

                        // Top spacer for rows before visible range
                        if (visibleRange.start > 0)
                        {
                            float topSpacerHeight = 0;
                            for (int i = 0; i < visibleRange.start; i++)
                            {
                                topSpacerHeight += rowHeights[i];
                            }
                            GUILayout.Space(topSpacerHeight);
                        }

                        // Render visible rows
                        for (int i = visibleRange.start; i < visibleRange.end; i++)
                        {
                            float rowHeight = rowHeights[i];
                            var rowRect = GUILayoutUtility.GetRect(0, rowHeight);
                            RenderTableRow(renderCtx, rowRect, allEntries.ElementAt(i), i, attributeCount, scheme,
                                tableCellControlIds, visibleEntryCount, visibleRange.start, rowHeight);
                            renderedCount++;
                        }

                        // Bottom spacer for rows after visible range
                        if (visibleRange.end < allEntries.Count)
                        {
                            float bottomSpacerHeight = 0;
                            for (int i = visibleRange.end; i < allEntries.Count; i++)
                            {
                                bottomSpacerHeight += rowHeights[i];
                            }
                            GUILayout.Space(bottomSpacerHeight);
                        }

                        // Update the cell count in VirtualTableView for debug display
                        int totalCellsDrawn = renderedCount * attributeCount; // Each row has attributeCount cells
                        _virtualTableView.UpdateCellCount(totalCellsDrawn);

                        // GUILayout.EndScrollView();
                        _tableBodyMarker.End();
                    }
                }

                if (Event.current.type == EventType.Repaint)
                {
                    lastScrollViewRect = GUILayoutUtility.GetLastRect();
                }
                
                // add new entry form
                // Special case manifest entry creation, we want to set it with valid data, wizard flow to prompt creation
                if (scheme.AttributeCount > 0 && !scheme.IsManifest)
                {
                    if (AddButton("Create New Entry", expandWidth: true, height: 50f))
                    {
                        // TODO: Convert to command
                        scheme.CreateNewEmptyEntry(new SchemaContext
                        {
                            Scheme = scheme,
                            Driver = "User_Create_Entry",
                        });
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
                        // Disabling left/right arrow key overriding, it messes with editing text
                        // case KeyCode.LeftArrow:
                        //     if (focusedIndex % attributeCount > 0)
                        //     {
                        //         nextFocusedIndex = focusedIndex - 1;
                        //     }
                        //     break;
                        // case KeyCode.RightArrow:
                        //     if (focusedIndex % attributeCount < attributeCount - 1)
                        //     {
                        //         nextFocusedIndex = focusedIndex + 1;
                        //     }
                        //     break;
                        case KeyCode.Return:
                        case KeyCode.KeypadEnter:
                            // try to go right if possible
                            if (focusedIndex + 1 < allEntries.Count * attributeCount)
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

        private SchemaResult RenderExportOptions(DataScheme scheme)
        {
            GenericMenu menu = new GenericMenu();
            var ctx = new SchemaContext
            {
                Driver = $"{nameof(RenderExportOptions)}"
            };

            if (!GetStorage(ctx).Try(out var storage, out var storageError))
            {
                return storageError.Cast();
            }

            foreach (var storageFormat in storage.AllFormats)
            {
                // do not allow exporting of Manifest Codegen for projects..
                // This is only true for the base project...
                // weird, so the Manifest that Schema itself uses may differ from the Manifest that developers will author...
                // I need to publish my manifest seperately from the manifest that is built for a project...
                            
                // Okay, csharp code gen is part of publishing flow
                if (storageFormat is CSharpSchemeStorageFormat)
                {
                    continue;
                }
                menu.AddItem(new GUIContent(storageFormat.Extension.ToUpper()), (bool)false,
                    (GenericMenu.MenuFunction)(() =>
                    {
                        LatestResponse = storageFormat.Export(scheme, new SchemaContext
                        {
                            Scheme = scheme,
                            Driver = "User_Export_Scheme",
                        });
                    }));
            }

            menu.ShowAsContext();
            return SchemaResult.Pass();
        }

        private void RenderTableHeader(SchemaContext context, int attributeCount, DataScheme scheme)
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
                            var attribute = scheme.GetAttribute(attributeIdx).Result;

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
                                var ctx = new SchemaContext
                                {
                                    Driver = "User_Add_Attribute",
                                    Scheme = scheme,
                                    AttributeName = newAttributeName
                                };
                                LatestResponse = scheme.AddAttribute(ctx, new AttributeDefinition
                                {
                                    AttributeName = newAttributeName,
                                    DataType = DataType.Text,
                                    DefaultValue = string.Empty,
                                    IsIdentifier = false,
                                    ColumnWidth = AttributeDefinition.DefaultColumnWidth,
                                });

                                newAttributeName =
                                    string.Empty; // clear out attribute name field since it's unlikely someone wants to make another attribute with the same name
                            }
                        }
                    }

                    // Filter row
                    using (new GUILayout.HorizontalScope())
                    {
                        // Filters are already in-memory; avoid reloading here
                        EditorGUILayout.LabelField("Filters:", RightAlignedLabelStyle,
                            GUILayout.Width(SETTINGS_WIDTH),
                            GUILayout.ExpandWidth(false));
                        // GUILayout.Space(SETTINGS_WIDTH); // for the row number column
                        for (var attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
                        {
                            var attribute = scheme.GetAttribute(attributeIdx).Result;
                            string filterValue = attributeFilters.TryGetValue(attribute.AttributeName, out var val)
                                ? val
                                : string.Empty;

                            switch (attribute.DataType) 
                            {
                                case ReferenceDataType refDataType:
                                    if (GUILayout.Button(filterValue, GUILayout.Width(attribute.ColumnWidth)))
                                    {
                                        var referenceEntryOptions = new GenericMenu();
                
                                        if (GetScheme(context, refDataType.ReferenceSchemeName).Try(out var refSchema))
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
            
            // TODO: Convert to command
            var ctx = new SchemaContext
            {
                Driver = "User_Move_Entry",
                Scheme = scheme,
            };
            rowOptionsMenu.AddItem(new GUIContent("Move To Top"), isDisabled: canMoveUp, () =>
            {
                scheme.MoveEntry(ctx, entry, 0);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Up"), isDisabled: canMoveUp, () =>
            {
                scheme.MoveUpEntry(ctx, entry);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddItem(new GUIContent("Move Down"), isDisabled: canMoveDown, () =>
            {
                scheme.MoveDownEntry(ctx, entry);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddItem(new GUIContent("Move To Bottom"), isDisabled: canMoveDown, () =>
            {
                scheme.MoveEntry(ctx, entry, entryCount - 1);
                OnSelectedSchemeChanged?.Invoke();
            });
            rowOptionsMenu.AddSeparator("");
            rowOptionsMenu.AddItem(new GUIContent("Delete Entry"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Schema", "Are you sure you want to delete this entry?", "Yes, delete this entry", "No, cancel"))
                {
                    var ctx = new SchemaContext
                    {
                        Driver = "User_Delete_Entry",
                        Scheme = scheme,
                    };
                    SubmitCommandRequest(new CommandRequest
                    {
                        Description = $"Delete entry ({entryIdx}) from Scheme {scheme.SchemeName}",
                        Command = new DeleteEntryCommand(ctx, scheme, entry),
                        OnRequestComplete = result =>
                        {
                            // if (result.IsSuccess)
                            // {
                            //     // LatestResponse = scheme.DeleteEntry(entry);
                            //     SaveDataScheme(scheme, alsoSaveManifest: false);
                            //     OnSelectedSchemeChanged?.Invoke();
                            // }
                        }
                    });
                }
            });
            rowOptionsMenu.ShowAsContext();
        }

        private void RenderAttributeColumnOptions(int attributeIdx, DataScheme scheme, AttributeDefinition attribute,
            int attributeCount)
        {
            var columnOptionsMenu = new GenericMenu();

            var ctx = new SchemaContext
            {
                Driver = "User_Attribute_Move",
                Scheme = scheme,
            };
            // attribute column ordering options
            columnOptionsMenu.AddItem(new GUIContent("Move To Front"), isDisabled: attributeIdx == 0, () =>
            {
                scheme.MoveAttributeRank(ctx, attribute, 0);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move Left"), isDisabled: attributeIdx == 0, () =>
            {
                scheme.IncreaseAttributeRank(ctx, attribute);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move Right"), isDisabled: attributeIdx == attributeCount - 1, () =>
            {
                scheme.DecreaseAttributeRank(ctx, attribute);
            });
            columnOptionsMenu.AddItem(new GUIContent("Move To Back"), isDisabled: attributeIdx == attributeCount - 1, () =>
            {
                scheme.MoveAttributeRank(ctx, attribute, attributeCount - 1);
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
                        var ctx = new SchemaContext
                        {
                            Driver = "User_Convert_Attribute_Type",
                            Scheme = scheme,
                            AttributeName = attribute.AttributeName
                        };
                        LatestResponse =
                            scheme.ConvertAttributeType(ctx, attributeName: attribute.AttributeName,
                                newType: builtInType);
                    });
                
                // List variant
                columnOptionsMenu.AddItem(new GUIContent($"Convert Type/List/{builtInType.TypeName}"), 
                    isMatchingType, // TODO: fix this matching type logic
                    () =>
                    {
                        var ctx = new SchemaContext
                        {
                            Driver = "User_Convert_Attribute_Type",
                            Scheme = scheme,
                            AttributeName = attribute.AttributeName
                        };
                        var newListDataType = new ListDataType(builtInType, ctx);
                        LatestResponse =
                            scheme.ConvertAttributeType(ctx, attributeName: attribute.AttributeName,
                                newType: newListDataType);
                    });
            }
            
            // handle list data type conversion
            // ... can convert to a List data type of any other type...?
            
                            
            columnOptionsMenu.AddSeparator("Convert Type");
            if (GetAllSchemes(ctx).Try(out var allSchemes))
            {
                // render reference type conversions
                foreach (var schemeName in allSchemes.OrderBy(s => s))
                {
                    if (GetScheme(ctx, schemeName).Try(out var dataSchema) 
                        && dataSchema.GetIdentifierAttribute().Try(out var identifierAttribute))
                    {
                        var referenceDataType = new ReferenceDataType(schemeName, identifierAttribute.AttributeName);
                        bool isMatchingType = referenceDataType.Equals(attribute.DataType);
                        columnOptionsMenu.AddItem(new GUIContent($"Convert Type/{referenceDataType.TypeName}"),
                            on: isMatchingType, 
                            () =>
                            {
                                var ctx = new SchemaContext
                                {
                                    Driver = "User_Convert_Attribute",
                                    AttributeName = attribute.AttributeName,
                                    Scheme = scheme,
                                };
                                LatestResponse =
                                    scheme.ConvertAttributeType(ctx, attributeName: attribute.AttributeName,
                                        newType: referenceDataType);
                            });
                        
                        // List variant
                        columnOptionsMenu.AddItem(new GUIContent($"Convert Type/List/{referenceDataType.TypeName}"),
                            on: isMatchingType, // TODO: fix this matching type logic
                            () =>
                            {
                                var ctx = new SchemaContext
                                {
                                    Driver = "User_Convert_Attribute",
                                    AttributeName = attribute.AttributeName,
                                    Scheme = scheme,
                                };
                                var newListDataType = new ListDataType(referenceDataType, ctx);
                                LatestResponse =
                                    scheme.ConvertAttributeType(ctx, attributeName: attribute.AttributeName,
                                        newType: newListDataType);
                            });
                    }
                }
            }
                            
            columnOptionsMenu.AddSeparator("");
                            
            columnOptionsMenu.AddItem(new GUIContent("Delete Attribute"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Schema", $"Are you sure you want to delete this attribute: {attribute.AttributeName}?", "Yes, delete this attribute", "No, cancel"))
                {
                    LatestResponse = scheme.DeleteAttribute(new SchemaContext
                    {
                        Driver = "User_Delete_Attribute",
                        AttributeName = attribute.AttributeName,
                        Scheme = scheme,
                    }, attribute);
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
        private void RenderDataCell(SchemaContext renderCtx, Rect cellRect, DataEntry entry, AttributeDefinition attribute, object entryValue, CellStyle cellStyle, int entryIdx, int attributeIdx, DataScheme scheme, int[] tableCellControlIds, int visibleEntryCount, int visibleRangeStart, int attributeCount)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = cellStyle.BackgroundColor;
            
            // For virtual scrolling, we need to map the global entry index to local visible index
            int localEntryIdx = _virtualTableView.IsVirtualScrollingActive
                ? entryIdx - visibleRangeStart
                : entryIdx;
            
            if (localEntryIdx >= 0 && localEntryIdx < visibleEntryCount)
            {
                int controlIdIndex = localEntryIdx * attributeCount + attributeIdx;
                if (controlIdIndex >= 0 && controlIdIndex < tableCellControlIds.Length)
                {
                    tableCellControlIds[controlIdIndex] = GUIUtility.GetControlID(FocusType.Passive);
                }
            }

            var updateCtx = new SchemaContext
            {
                Driver = "User_Update_Entry_Value",
                Scheme = scheme,
            };
            
            // Render based on data type
            switch (attribute.DataType)
            {
                case IntegerDataType _:
                    RenderIntegerCell(cellRect, Convert.ToInt32(entryValue), cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case FloatingPointDataType _:
                    RenderFloatCell(cellRect, Convert.ToSingle(entryValue), cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
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
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case FilePathDataType _:
                    RenderFilePathCell(cellRect, entryValue.ToString(), cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case FolderDataType _:
                    RenderFolderCell(cellRect, entryValue.ToString(), cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case ReferenceDataType refDataType:
                    RenderReferenceCell(renderCtx, cellRect, entryValue, refDataType, cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case DateTimeDataType _:
                    RenderDateTimeCell(cellRect, entryValue is DateTime dt ? dt : DateTime.Now, cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case TextDataType _:
                    RenderTextFieldCell(cellRect, entryValue, cellStyle, value => 
                        UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case UnityAssetDataType unityAssetDataType:
                    RenderAssetCell(unityAssetDataType, cellRect, entryValue is Guid ? (Guid)entryValue : default, cellStyle,
                        value => UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case GuidDataType _:
                    RenderGuidCell(cellRect, entryValue is Guid ? (Guid)entryValue : default, cellStyle,
                        value => UpdateEntryValue(updateCtx, entry, attribute, value, scheme));
                    break;
                case ListDataType listDataType:
                    RenderListCell(renderCtx, cellRect, entryValue, listDataType, cellStyle, value => UpdateEntryValue(updateCtx, entry,  attribute, value, scheme));
                    break;
                default:
                    RenderUnmappedFieldCell(cellRect, entryValue, cellStyle);
                    break;
            }
            
            GUI.backgroundColor = originalColor;
        }

        /// <summary>
        /// Renders a list data type cell with add/remove functionality
        /// </summary>
        private void RenderListCell(SchemaContext context, Rect cellRect, object entryValue, ListDataType listDataType, CellStyle cellStyle, Action<object> onValueChanged)
        {
            // Convert entry value to a list we can work with
            var listItems = new List<object>();
            if (entryValue != null && entryValue is IEnumerable enumerable && !(entryValue is string))
            {
                foreach (var item in enumerable)
                {
                    listItems.Add(item);
                }
            }

            const float itemHeight = 20f;
            const float buttonWidth = 20f;
            const float spacing = 2f;
            const float headerHeight = 22f;
            
            // Draw background
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            GUI.Box(cellRect, "", EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;
            
            float currentY = cellRect.y + spacing;
            
            // Header with count and add button
            var headerRect = new Rect(cellRect.x + spacing, currentY, cellRect.width - spacing * 2, headerHeight);
            var addButtonRect = new Rect(headerRect.xMax - buttonWidth, headerRect.y, buttonWidth, headerHeight);
            var headerLabelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - buttonWidth - spacing, headerHeight);
            
            EditorGUI.LabelField(headerLabelRect, $"{listItems.Count} item{(listItems.Count != 1 ? "s" : "")}", EditorStyles.miniLabel);
            
            if (GUI.Button(addButtonRect, "+", EditorStyles.miniButton))
            {
                // Add a new default item
                object newItem = GetDefaultValueForElementType(context, listDataType.ElementType);
                listItems.Add(newItem);
                onValueChanged?.Invoke(ConvertListToTypedArray(listItems, listDataType.ElementType));
            }
            
            currentY += headerHeight + spacing;
            
            // Render each list item
            for (int i = 0; i < listItems.Count; i++)
            {
                var itemRect = new Rect(cellRect.x + spacing, currentY, cellRect.width - spacing * 2, itemHeight);
                var removeButtonRect = new Rect(itemRect.xMax - buttonWidth, itemRect.y, buttonWidth, itemHeight);
                var valueRect = new Rect(itemRect.x, itemRect.y, itemRect.width - buttonWidth - spacing, itemHeight);
                
                // Render the value field based on element type
                object newValue = RenderListElementField(context, valueRect, listItems[i], listDataType.ElementType, cellStyle);
                if (newValue != null && !Equals(newValue, listItems[i]))
                {
                    listItems[i] = newValue;
                    onValueChanged?.Invoke(ConvertListToTypedArray(listItems, listDataType.ElementType));
                }
                
                // Remove button
                if (GUI.Button(removeButtonRect, "-", EditorStyles.miniButton))
                {
                    listItems.RemoveAt(i);
                    onValueChanged?.Invoke(ConvertListToTypedArray(listItems, listDataType.ElementType));
                    break; // Exit the loop since we modified the list
                }
                
                currentY += itemHeight + spacing;
            }
        }
        
        /// <summary>
        /// Renders a field for a single list element based on its data type
        /// </summary>
        private object RenderListElementField(SchemaContext context, Rect rect, object value, DataType elementType, CellStyle cellStyle)
        {
            if (elementType == null)
            {
                return EditorGUI.TextField(rect, value?.ToString() ?? "", cellStyle.FieldStyle);
            }
            
            switch (elementType)
            {
                case IntegerDataType _:
                    int intValue = value != null ? Convert.ToInt32(value) : 0;
                    return SchemaGUI.IntField(rect, intValue, cellStyle.FieldStyle);
                    
                case FloatingPointDataType _:
                    float floatValue = value != null ? Convert.ToSingle(value) : 0f;
                    return SchemaGUI.FloatField(rect, floatValue, cellStyle.FieldStyle);
                    
                case BooleanDataType _:
                    bool boolValue = false;
                    if (value is bool b)
                        boolValue = b;
                    else if (value is string s && bool.TryParse(s, out var parsed))
                        boolValue = parsed;
                    else if (value is int i)
                        boolValue = i != 0;
                    return EditorGUI.Toggle(rect, boolValue);
                    
                case ReferenceDataType refDataType:
                    var currentValue = value?.ToString() ?? "...";
                    if (GUI.Button(rect, currentValue, cellStyle.DropdownStyle))
                    {
                        var referenceEntryOptions = new GenericMenu();
                        if (GetScheme(context, refDataType.ReferenceSchemeName).Try(out var refSchema))
                        {
                            foreach (var identifierValue in refSchema.GetIdentifierValues())
                            {
                                var idValueStr = identifierValue.ToString();
                                referenceEntryOptions.AddItem(
                                    new GUIContent(idValueStr),
                                    on: identifierValue.Equals(currentValue),
                                    () => { /* Value will be updated on next frame */ });
                            }
                        }
                        referenceEntryOptions.ShowAsContext();
                    }
                    return value; // Reference values need special handling
                    
                case GuidDataType _:
                    Guid guidValue = value is Guid g ? g : Guid.Empty;
                    return EditorGUI.TextField(rect, guidValue.ToString(), cellStyle.FieldStyle);
                    
                case DateTimeDataType _:
                    DateTime dateValue = value is DateTime dt ? dt : DateTime.Now;
                    string dateStr = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                    string newDateStr = EditorGUI.TextField(rect, dateStr, cellStyle.FieldStyle);
                    if (newDateStr != dateStr && DateTime.TryParse(newDateStr, out DateTime parsedDate))
                    {
                        return parsedDate;
                    }
                    return dateValue;
                    
                case TextDataType _:
                case FilePathDataType _:
                case FolderDataType _:
                default:
                    return EditorGUI.TextField(rect, value?.ToString() ?? "", cellStyle.FieldStyle);
            }
        }
        
        /// <summary>
        /// Gets the default value for a given element type
        /// </summary>
        private object GetDefaultValueForElementType(SchemaContext context, DataType elementType)
        {
            if (elementType == null)
                return "";
            
            switch (elementType)
            {
                case IntegerDataType _:
                    return 0;
                case FloatingPointDataType _:
                    return 0f;
                case BooleanDataType _:
                    return false;
                case GuidDataType _:
                    return Guid.NewGuid();
                case DateTimeDataType _:
                    return DateTime.Now;
                case ReferenceDataType refDataType:
                    // Try to get the first identifier value from the referenced scheme
                    if (GetScheme(context, refDataType.ReferenceSchemeName).Try(out var refSchema))
                    {
                        var identifiers = refSchema.GetIdentifierValues().ToArray();
                        if (identifiers.Length > 0)
                            return identifiers[0];
                    }
                    return "";
                case TextDataType _:
                case FilePathDataType _:
                case FolderDataType _:
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// Converts a generic list to a typed array based on element type
        /// </summary>
        private object ConvertListToTypedArray(List<object> list, DataType elementType)
        {
            if (elementType == null)
                return list.ToArray();
            
            switch (elementType)
            {
                case IntegerDataType _:
                    return list.Select(o => Convert.ToInt32(o)).ToArray();
                case FloatingPointDataType _:
                    return list.Select(o => Convert.ToSingle(o)).ToArray();
                case BooleanDataType _:
                    return list.Select(o => o is bool b ? b : Convert.ToBoolean(o)).ToArray();
                case GuidDataType _:
                    return list.Select(o => o is Guid g ? g : Guid.Parse(o.ToString())).ToArray();
                case DateTimeDataType _:
                    return list.Select(o => o is DateTime dt ? dt : DateTime.Parse(o.ToString())).ToArray();
                case ReferenceDataType _:
                case TextDataType _:
                case FilePathDataType _:
                case FolderDataType _:
                    return list.Select(o => o?.ToString() ?? "").ToArray();
                default:
                    return list.ToArray();
            }
        }

        private void RenderGuidCell(Rect cellRect, Guid entryValue, CellStyle cellStyle, Action<object> action)
        {
            // var assetPath = AssetDatabase.GUIDToAssetPath(entryValue.ToString().Replace("-", string.Empty));
            // var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            EditorGUI.TextField(cellRect, entryValue.ToString(), cellStyle.FieldStyle);
        }

        private void RenderAssetCell(UnityAssetDataType assetDataType, Rect cellRect, Guid entryValue, CellStyle cellStyle, Action<string> onValueChanged)
        {
            // var assetPath = AssetDatabase.GUIDToAssetPath(entryValue.ToString().Replace("-", string.Empty));
            // var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Type assetType = assetDataType.ObjectType;
            if (AssetUtils.TryLoadAssetFromGUID(entryValue, out var asset))
            {
                assetType = asset.GetType();
            }
            
            var newAsset = EditorGUI.ObjectField(cellRect, asset, assetType, allowSceneObjects: false);
            if (newAsset != asset)
            {
                if (newAsset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newAsset, out var guid, out long _))
                {
                    onValueChanged?.Invoke(guid);
                }
                else
                {
                    onValueChanged?.Invoke(null);
                }
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
            if (!Mathf.Approximately(newValue, value))
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
            var filePreview = filePath;
            var previewPath = string.Empty;
            if (string.IsNullOrEmpty(filePreview))
            {
                filePreview = "...";
            }
            else
            {
                previewPath = filePath;
            }
            
            var fileContent = new GUIContent(filePreview, tooltip: filePath);
            if (GUI.Button(cellRect, fileContent, cellStyle.ButtonStyle))
            {
                var selectedFilePath = EditorUtility.OpenFilePanel("Schema - File Selection", previewPath, "");
                if (!string.IsNullOrEmpty(selectedFilePath))
                {
                    onValueChanged?.Invoke(selectedFilePath);
                }
            }
        }
        
        /// <summary>
        /// Renders a Folder path cell
        /// </summary>
        private void RenderFolderCell(Rect cellRect, string folderPath, CellStyle cellStyle, Action<string> onValueChanged)
        {
            string folderPreview = PathUtility.SanitizePath(folderPath);
            var previewPath = string.Empty;
            if (string.IsNullOrEmpty(folderPreview))
            {
                folderPreview = "...";
            }
            else
            {
                previewPath = folderPreview;
            }
            
            var folderContent = new GUIContent(folderPreview, tooltip: folderPath);
            if (GUI.Button(cellRect, folderContent, cellStyle.ButtonStyle))
            {
                var selectedFolder = EditorUtility.OpenFolderPanel("Schema - Folder Selection", previewPath, "");
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    onValueChanged?.Invoke(selectedFolder);
                }
            }
        }
        
        /// <summary>
        /// Renders a reference data type cell
        /// </summary>
        private void RenderReferenceCell(SchemaContext context, Rect cellRect, object value, ReferenceDataType refDataType, CellStyle cellStyle, Action<object> onValueChanged)
        {
            var gotoButtonWidth = 20f;
            var refDropdownWidth = cellRect.width - gotoButtonWidth;
            var currentValue = value?.ToString() ?? "...";
            
            var dropdownRect = new Rect(cellRect.x, cellRect.y, refDropdownWidth, cellRect.height);
            var gotoRect = new Rect(cellRect.x + refDropdownWidth, cellRect.y, gotoButtonWidth, cellRect.height);
            
            if (GUI.Button(dropdownRect, currentValue, cellStyle.DropdownStyle))
            {
                var referenceEntryOptions = new GenericMenu();
                
                if (GetScheme(context, refDataType.ReferenceSchemeName).Try(out var refSchema))
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
                var ctx = new SchemaContext
                {
                    Driver = "User_Focus_Object_Reference",
                    DataType = refDataType.TypeName
                };
                FocusOnEntry(ctx, refDataType.ReferenceSchemeName, 
                    refDataType.ReferenceAttributeName, 
                    currentValue);
                ReleaseControlFocus();
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
        private void UpdateEntryValue(SchemaContext context, DataEntry entry, AttributeDefinition attribute, object newValue, DataScheme scheme)
        {
            var attributeName = attribute.AttributeName;
            
            if (attribute.IsIdentifier)
            {
                // sanitize identifier input
                if (newValue == null)
                {
                    newValue = string.Empty;
                }
                
                if (string.IsNullOrWhiteSpace(newValue.ToString()))
                {
                    return;
                }
                
                if (scheme.GetIdentifierValues().Any(otherAttributeName => Equals(otherAttributeName, newValue)))
                {
                    EditorUtility.DisplayDialog("Schema", $"Attribute '{attribute.AttributeName}' with value '{newValue}' already exists.", "OK");
                    return;
                }

                var oldValue = entry.GetData(attributeName);
                Logger.LogDbgVerbose($"{scheme}, {entry}, old Value: {oldValue} for attribute: {attributeName}");
                var idRes = UpdateIdentifierValue(context, scheme.SchemeName, attributeName, oldValue, newValue);
                if (idRes.Failed)
                {
                    Logger.LogDbgError(idRes.Message, idRes.Context);
                }
            }
            else
            {
                _ = ExecuteSetDataOnEntryAsync(context, scheme, entry, attributeName, newValue);
            }
        }
        
        /// <summary>
        /// Renders a single table row using rect-based rendering
        /// </summary>
        private void RenderTableRow(SchemaContext renderCtx, Rect rowRect, DataEntry entry, int entryIdx, int attributeCount, DataScheme scheme, int[] tableCellControlIds, int visibleEntryCount, int visibleRangeStart, float rowHeight)
        {
            var cellStyle = GetRowCellStyle(entryIdx);
            
            // Render row background
            RenderRowBackground(rowRect, cellStyle);
            
            // Calculate cell positions relative to the row rect
            float currentX = rowRect.x;
            
            // Render row number cell
            var rowNumberRect = new Rect(currentX, rowRect.y, _tableLayout.SettingsWidth, rowHeight);
            RenderRowNumberCell(rowNumberRect, entryIdx + 1, cellStyle, entryIdx, scheme.EntryCount, scheme, entry);
            currentX += _tableLayout.SettingsWidth;
            
            // Render data cells
            for (int attributeIdx = 0; attributeIdx < attributeCount; attributeIdx++)
            {
                var attribute = scheme.GetAttribute(attributeIdx).Result;
                var cellRect = new Rect(currentX, rowRect.y, attribute.ColumnWidth, rowHeight);
                var entryValue = GetEntryValue(scheme, entry, attribute);
                
                using (_dataCellMarker.Auto()) 
                {
                    RenderDataCell(renderCtx, cellRect, entry, attribute, entryValue, cellStyle, entryIdx, attributeIdx, scheme, tableCellControlIds, visibleEntryCount, visibleRangeStart, attributeCount);
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

            var ctx = new SchemaContext
            {
                Driver = "Auto_Migrate_Entry",
                Scheme = scheme
            };
            if (entryValue == null)
            {
                var defaultValue = attribute.CloneDefaultValue();
                if (defaultValue != null)
                {
                    entry.SetData(ctx, attribute.AttributeName, entryValue);
                }
            }
            else if (attribute.CheckIfValidData(ctx, entryValue).Failed)
            {
                using (_dataConvertMarker.Auto())
                {
                    if (DataType.ConvertValue(ctx, entryValue, DataType.Default, attribute.DataType).Try(out var convertedValue))
                    {
                        entryValue = convertedValue;
                        entry.SetData(ctx, attribute.AttributeName, entryValue);
                    }
                    else
                    {
                        entryValue = attribute.CloneDefaultValue();
                        entry.SetData(ctx, attribute.AttributeName, entryValue);
                    }
                }
            }
            
            return entryValue;
        }
        
        #endregion
    }
}