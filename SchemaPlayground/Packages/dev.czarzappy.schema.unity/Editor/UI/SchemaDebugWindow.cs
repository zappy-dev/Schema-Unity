using System;
using System.Linq;
using Schema.Core;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public class SchemaDebugWindow : EditorWindow
    {
        private void OnGUI()
        {
            RenderDebugView();
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("Logging");
            EditorGUILayout.EnumPopup("Log Level", Logger.Level);
        }

        private void RenderDebugView()
        {
            RenderSchemaDebugSettings();
            
            using (new EditorGUI.DisabledScope())
            {
                EditorGUILayout.Toggle("Is Schema Initialized?", IsInitialized);
                EditorGUILayout.Toggle("Is Load In Progress?", IsManifestLoadInProgress);


                using (var guiEventScroll = new EditorGUILayout.ScrollViewScope(guiEventsScrollPos))
                {
                    guiEventsScrollPos = guiEventScroll.scrollPosition;
                    if (IsInitialized)
                    {
                        EditorGUILayout.IntField("Num available schemes", AllSchemes.Count());

                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (var scheme in AllSchemes)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    EditorGUILayout.TextField(scheme);
                                    var getRes = GetScheme(scheme);
                                    EditorGUILayout.Toggle("Loaded?", getRes.Passed);
                                    if (getRes.Failed)
                                    {
                                        EditorGUILayout.HelpBox(getRes.Message, MessageType.Error);
                                    }
                                }
                            }

                            foreach (var loadedScheme in LoadedSchemes)
                            {
                                EditorGUILayout.TextField(loadedScheme.Value.ToString());
                            }
                        }
                    }

                    // Virtual scrolling debug info (compact)
                    // if (_virtualTableView != null)
                    // {
                    //     EditorGUILayout.Space();
                    //     EditorGUILayout.LabelField("Virtual Scrolling Debug:", EditorStyles.boldLabel);
                    //     
                    //     using (new EditorGUILayout.HorizontalScope())
                    //     {
                    //         // Left column - Core info
                    //         using (new EditorGUILayout.VerticalScope())
                    //         {
                    //             EditorGUILayout.Toggle("Virtual Scrolling Active", _virtualTableView.IsVirtualScrollingActive);
                    //             if (_virtualTableView.IsVirtualScrollingActive)
                    //             {
                    //                 var range = _virtualTableView.VisibleRange;
                    //                 EditorGUILayout.LabelField($"Range: {range.start}-{range.end}");
                    //                 EditorGUILayout.LabelField($"Scroll: {tableViewBodyVerticalScrollPosition.y:F0}");
                    //                 EditorGUILayout.LabelField($"Cells Drawn: {_virtualTableView.CellsDrawn}");
                    //                 
                    //                 if (selectedSchemeName != null && GetScheme(selectedSchemeName).Try(out var scheme))
                    //                 {
                    //                     var allEntries = scheme.GetEntries().ToList();
                    //                     int totalCells = allEntries.Count * scheme.AttributeCount;
                    //                     EditorGUILayout.LabelField($"Total Cells: {totalCells}");
                    //                     EditorGUILayout.LabelField($"Efficiency: {(_virtualTableView.CellsDrawn * 100f / Math.Max(1, totalCells)):F1}%");
                    //                     
                    //                     float expectedScrollY = range.start * (_virtualTableView.TotalContentHeight / Math.Max(1, allEntries.Count));
                    //                     EditorGUILayout.LabelField($"Delta: {tableViewBodyVerticalScrollPosition.y - expectedScrollY:F0}");
                    //                     EditorGUILayout.LabelField($"Progress: {(tableViewBodyVerticalScrollPosition.y / Math.Max(1, _virtualTableView.TotalContentHeight)) * 100:F0}%");
                    //                 }
                    //             }
                    //         }
                    //         
                    //         // Right column - Warnings and errors
                    //         using (new EditorGUILayout.VerticalScope())
                    //         {
                    //             if (selectedSchemeName != null && GetScheme(selectedSchemeName).Try(out var scheme))
                    //             {
                    //                 var allEntries = scheme.GetEntries().ToList();
                    //                 var visibleRange = _virtualTableView.VisibleRange;
                    //                 
                    //                 EditorGUILayout.LabelField($"Entries: {allEntries.Count}");
                    //                 
                    //                 if (visibleRange.end > allEntries.Count)
                    //                 {
                    //                     EditorGUILayout.LabelField($"⚠️ OVERFLOW: {visibleRange.end}>{allEntries.Count}", EditorStyles.boldLabel);
                    //                 }
                    //                 
                    //                 if (visibleRange.start >= visibleRange.end)
                    //                 {
                    //                     EditorGUILayout.LabelField($"⚠️ INVALID: {visibleRange.start}>={visibleRange.end}", EditorStyles.boldLabel);
                    //                 }
                    //                 
                    //                 if (Math.Abs(tableViewBodyVerticalScrollPosition.y - (visibleRange.start * (_virtualTableView.TotalContentHeight / Math.Max(1, allEntries.Count)))) > 50)
                    //                 {
                    //                     EditorGUILayout.LabelField($"⚠️ SCROLL MISMATCH", EditorStyles.boldLabel);
                    //                 }
                    //             }
                    //         }
                    //     }
                    // }

                    // Performance monitoring info
                    // _performanceMonitor?.RenderDebugInfo();
                }

                if (GUILayout.Button("Fix Duplicate Entries"))
                {
                    var ctx = new SchemaContext
                    {
                        Driver = "Debug_User_Fix_Duplicate_Entries"
                    };
                    foreach (var schemeName in AllSchemes)
                    {
                        if (!GetScheme(schemeName).Try(out var scheme))
                        {
                            continue; // can't de-dupe these schemes easily
                        }

                        if (!scheme.GetIdentifierAttribute().Try(out var identifierAttribute))
                        {
                            identifierAttribute = scheme.GetAttribute(0).Result; // just use first attribute
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
                                scheme.DeleteEntry(ctx, entry);
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
                            SaveDataScheme(ctx, scheme, false);
                        }
                    }
                }

                RenderGUIEvents();
            }
        }

        private void RenderSchemaDebugSettings()
        {
            // Controls for enabling / disabling debug mode
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            if (defines.Contains("SCHEMA_DEBUG"))
            {
                if (GUILayout.Button("Disable Schema Debug Mode"))
                {
                    var newDefines = string.Join(";", defines.Split(';').Where(d => d != "SCHEMA_DEBUG"));
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
                    LogDbgVerbose("Removed SCHEMA_DEBUG scripting define.");
                }
            }
            else
            {
                if (GUILayout.Button("Enable Schema Debug Mode"))
                {

                    if (!string.IsNullOrEmpty(defines))
                        defines += ";";
                    defines += "SCHEMA_DEBUG";
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
                    LogDbgVerbose("Added SCHEMA_DEBUG scripting define.");
                }
            }

        }

        private Vector2 guiEventsScrollPos = Vector2.zero;
        private int schemeIdx = 0;
        private void RenderGUIEvents()
        {
            EditorGUILayout.LabelField("GUI Events");
                
            // GetWin
            if (SchemaEditorWindow.Instance != null)
            {
                if (GUILayout.Button("Sim Click - Create New Scheme"))
                {
                    var coroutine = SchemaEditorWindow.Instance.SimClick_CreateNewSchema();
                    EditorCoroutineUtility.StartCoroutine(coroutine, this);
                }

                schemeIdx = EditorGUILayout.IntField("Scheme Index", schemeIdx);
                
                if (GUILayout.Button("Sim Click - Explorer Scheme"))
                {
                    var coroutine = SchemaEditorWindow.Instance.SimClick_ExplorerSelectSchemeByIndex(schemeIdx);
                    EditorCoroutineUtility.StartCoroutine(coroutine, this);
                }
                
                EditorGUILayout.TextArea(SchemaEditorWindow.Instance.ReportEvents());
            }
            else
            {
                if (GUILayout.Button("Open Editor"))
                {
                    EditorWindowExt.Open<SchemaEditorWindow>();
                }
            }
        }
    }
}