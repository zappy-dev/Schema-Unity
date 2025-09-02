using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        private void RenderDebugView()
        {
            using (new EditorGUI.DisabledScope())
            {
                EditorGUILayout.Toggle("Is Schema Initialized?", IsInitialized);
                EditorGUILayout.Toggle("Is Load In Progress?", IsManifestLoadInProgress);
                EditorGUILayout.IntField("Num available schemes", AllSchemes.Count());
                
                // Virtual scrolling debug info (compact)
                if (_virtualTableView != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Virtual Scrolling Debug:", EditorStyles.boldLabel);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // Left column - Core info
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.Toggle("Virtual Scrolling Active", _virtualTableView.IsVirtualScrollingActive);
                            if (_virtualTableView.IsVirtualScrollingActive)
                            {
                                var range = _virtualTableView.VisibleRange;
                                EditorGUILayout.LabelField($"Range: {range.start}-{range.end}");
                                EditorGUILayout.LabelField($"Scroll: {tableViewBodyVerticalScrollPosition.y:F0}");
                                EditorGUILayout.LabelField($"Cells Drawn: {_virtualTableView.CellsDrawn}");
                                
                                if (selectedSchemeName != null && GetScheme(selectedSchemeName).Try(out var scheme))
                                {
                                    var allEntries = scheme.GetEntries().ToList();
                                    int totalCells = allEntries.Count * scheme.AttributeCount;
                                    EditorGUILayout.LabelField($"Total Cells: {totalCells}");
                                    EditorGUILayout.LabelField($"Efficiency: {(_virtualTableView.CellsDrawn * 100f / Math.Max(1, totalCells)):F1}%");
                                    
                                    float expectedScrollY = range.start * (_virtualTableView.TotalContentHeight / Math.Max(1, allEntries.Count));
                                    EditorGUILayout.LabelField($"Delta: {tableViewBodyVerticalScrollPosition.y - expectedScrollY:F0}");
                                    EditorGUILayout.LabelField($"Progress: {(tableViewBodyVerticalScrollPosition.y / Math.Max(1, _virtualTableView.TotalContentHeight)) * 100:F0}%");
                                }
                            }
                        }
                        
                        // Right column - Warnings and errors
                        using (new EditorGUILayout.VerticalScope())
                        {
                            if (selectedSchemeName != null && GetScheme(selectedSchemeName).Try(out var scheme))
                            {
                                var allEntries = scheme.GetEntries().ToList();
                                var visibleRange = _virtualTableView.VisibleRange;
                                
                                EditorGUILayout.LabelField($"Entries: {allEntries.Count}");
                                
                                if (visibleRange.end > allEntries.Count)
                                {
                                    EditorGUILayout.LabelField($"⚠️ OVERFLOW: {visibleRange.end}>{allEntries.Count}", EditorStyles.boldLabel);
                                }
                                
                                if (visibleRange.start >= visibleRange.end)
                                {
                                    EditorGUILayout.LabelField($"⚠️ INVALID: {visibleRange.start}>={visibleRange.end}", EditorStyles.boldLabel);
                                }
                                
                                if (Math.Abs(tableViewBodyVerticalScrollPosition.y - (visibleRange.start * (_virtualTableView.TotalContentHeight / Math.Max(1, allEntries.Count)))) > 50)
                                {
                                    EditorGUILayout.LabelField($"⚠️ SCROLL MISMATCH", EditorStyles.boldLabel);
                                }
                            }
                        }
                    }
                }
                
                // Performance monitoring info
                // _performanceMonitor?.RenderDebugInfo();
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Manifest Path");
                
                using (new EditorGUI.DisabledScope())
                {
                    EditorGUILayout.TextField("Manifest Import Path", ManifestImportPath);
                    EditorGUILayout.IntField("Loaded Manifest Scheme Hash", RuntimeHelpers.GetHashCode(LoadedManifestScheme._));
                }

                if (GUILayout.Button("Load"))
                {
                    OnLoadManifest("On User Load");
                }

                // save schemes to manifest
                if (GUILayout.Button("Save Manifest"))
                {
                    LatestResponse = SaveManifest();
                }
            }

            if (GUILayout.Button("Fix Duplicate Entries"))
            {
                foreach (var schemeName in AllSchemes)
                {
                    if (!GetScheme(schemeName).Try(out var scheme))
                    {
                        continue; // can't de-dupe these schemes easily
                    }

                    if (!scheme.GetIdentifierAttribute().Try(out var identifierAttribute))
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
                        LogWarning($"Scheme '{schemeName}' has deleted {numDeleted} entries.");
                        SaveDataScheme(scheme, false);
                    }
                }
            }

            if (LatestManifestLoadResponse.Message != null)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestManifestLoadResponse.Result}: {LatestManifestLoadResponse.Message}", LatestManifestLoadResponse.MessageType());
            }
            
            if (LatestResponse.Message != null)
            {
                EditorGUILayout.HelpBox($"[{latestResponseTime:T}] {LatestResponse.Message}", LatestResponse.MessageType());
            }

            // Controls for enabling / disabling debug mode
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            if (defines.Contains("SCHEMA_DEBUG"))
            {
                if (GUILayout.Button("Remove SCHEMA_DEBUG Scripting Define"))
                {
                    var newDefines = string.Join(";", defines.Split(';').Where(d => d != "SCHEMA_DEBUG"));
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
                    LogDbgVerbose("Removed SCHEMA_DEBUG scripting define.");
                }
            }
            else
            {
                if (GUILayout.Button("Add SCHEMA_DEBUG Scripting Define"))
                {
                    
                    if (!string.IsNullOrEmpty(defines))
                        defines += ";";
                    defines += "SCHEMA_DEBUG";
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defines);
                    LogDbgVerbose("Added SCHEMA_DEBUG scripting define.");
                }
            }
        }
    }
}