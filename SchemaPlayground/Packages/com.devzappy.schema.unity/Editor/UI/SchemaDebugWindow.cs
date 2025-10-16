using System;
using System.Linq;
using Schema.Core;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaContext;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public class SchemaDebugWindow : EditorWindow
    {
        private const string DEBUG_MODE_SCRIPTING_DEFINE = "SCHEMA_DEBUG";
        private const string PERF_MODE_SCRIPTING_DEFINE = "SCHEMA_PERF";
        
        private void OnGUI()
        {
            var renderCtx = EditContext.WithDriver($"{nameof(SchemaDebugWindow)}_Render");
            
            RenderDebugView(renderCtx);
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("Logging");
            EditorGUILayout.EnumPopup("Log Level", Logger.Level);
        }

        private void RenderDebugView(SchemaContext ctx)
        {
            RenderSchemaDebugSettings();
            
            using (new EditorGUI.DisabledScope())
            {
                var isInitRes = IsInitialized(ctx);
                var isInit = isInitRes.Passed;
                EditorGUILayout.HelpBox(isInitRes.Message, (isInitRes.Failed) ? MessageType.Error : MessageType.Info);
                EditorGUILayout.Toggle("Is Schema Initialized?", isInit);
                if (isInit)
                {
                    EditorGUILayout.TextField("Project Path", ctx.Project.ProjectPath);
                    EditorGUILayout.TextField("Default Content Directory", DefaultContentDirectory);
                    EditorGUILayout.TextField("Default Content", ctx.Project.DefaultContentPath);
                    EditorGUILayout.Toggle("Is Load In Progress?", IsManifestLoadInProgress);
                }
                
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Project Container");

                var isLoaded = LatestProject != null;
                EditorGUILayout.Toggle("Is Loaded?", isLoaded);
                if (isLoaded)
                {
                    EditorGUILayout.TextField("Loaded Project", LatestProject.ToString());
                }
                
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Schemes");
                using (var guiEventScroll = new EditorGUILayout.ScrollViewScope(guiEventsScrollPos))
                {
                    guiEventsScrollPos = guiEventScroll.scrollPosition;
                    if (isInit)
                    {
                        if (!GetAllSchemes(ctx).Try(out var allSchemes, out var allSchemesError))
                        {
                            EditorGUILayout.HelpBox(allSchemesError.Message, MessageType.Error);
                        }
                        else
                        {
                            EditorGUILayout.IntField("Num available schemes", allSchemes.Count());

                            using (new EditorGUI.IndentLevelScope())
                            {
                                foreach (var scheme in allSchemes)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        EditorGUILayout.TextField(scheme);
                                        var getRes = GetScheme(ctx, scheme);
                                        EditorGUILayout.Toggle("Loaded?", getRes.Passed);
                                        if (getRes.Failed)
                                        {
                                            EditorGUILayout.HelpBox(getRes.Message, MessageType.Error);
                                        }
                                    }
                                }

                                if (isLoaded)
                                {
                                    foreach (var loadedScheme in LatestProject.Schemes)
                                    {
                                        EditorGUILayout.TextField(loadedScheme.Value.ToString(false));
                                    }
                                }
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
                    FixDuplicateEntries();
                }

                showGUIEvents = EditorGUILayout.Toggle("Show GUI Events", showGUIEvents);
                if (showGUIEvents)
                {
                    RenderGUIEvents();
                }
            }
        }

        private static SchemaResult FixDuplicateEntries()
        {
            var ctx = EditContext.WithDriver("Debug_User_Fix_Duplicate_Entries");
            
            if (!GetAllSchemes(ctx).Try(out var allSchemes, out var allSchemesError)) return allSchemesError.Cast();

            foreach (var schemeName in allSchemes)
            {
                if (!GetScheme(ctx, schemeName).Try(out var scheme))
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
                    if (!SaveDataScheme(ctx, scheme, false).Try(out var err)) return err;
                }
            }
            
            return SchemaResult.Pass();
        }

        private void RenderSchemaDebugSettings()
        {
            // Controls for enabling / disabling debug mode
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            if (defines.Contains(DEBUG_MODE_SCRIPTING_DEFINE))
            {
                if (GUILayout.Button("Disable Schema Debug Mode"))
                {
                    var newDefines = string.Join(";", defines.Split(';').Where(d => d != DEBUG_MODE_SCRIPTING_DEFINE));
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
                    LogDbgVerbose($"Removed {DEBUG_MODE_SCRIPTING_DEFINE} scripting define.");
                }
            }
            else
            {
                if (GUILayout.Button("Enable Schema Debug Mode"))
                {

                    if (!string.IsNullOrEmpty(defines))
                        defines += ";";
                    defines += DEBUG_MODE_SCRIPTING_DEFINE;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
                    LogDbgVerbose($"Added {DEBUG_MODE_SCRIPTING_DEFINE} scripting define.");
                }
            }
            
            if (defines.Contains(PERF_MODE_SCRIPTING_DEFINE))
            {
                if (GUILayout.Button("Disable Schema Perf Mode"))
                {
                    var newDefines = string.Join(";", defines.Split(';').Where(d => d != PERF_MODE_SCRIPTING_DEFINE));
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
                    LogDbgVerbose($"Removed {PERF_MODE_SCRIPTING_DEFINE} scripting define.");
                }
            }
            else
            {
                if (GUILayout.Button("Enable Schema Perf Mode"))
                {

                    if (!string.IsNullOrEmpty(defines))
                        defines += ";";
                    defines += PERF_MODE_SCRIPTING_DEFINE;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
                    LogDbgVerbose($"Added {PERF_MODE_SCRIPTING_DEFINE} scripting define.");
                }
            }

        }

        private Vector2 guiEventsScrollPos = Vector2.zero;
        private int schemeIdx = 0;
        private bool showGUIEvents;

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
                    var ctx = EditContext.WithDriver("Sim_Click_Explorer_Scheme");
                    var coroutine = SchemaEditorWindow.Instance.SimClick_ExplorerSelectSchemeByIndex(ctx, schemeIdx);
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