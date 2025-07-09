using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;
using Logger = Schema.Core.Logger;

namespace Schema.Unity.Editor
{
    public partial class SchemaEditorWindow
    {
        private void RenderDebugView()
        {
            using (new EditorGUI.DisabledScope())
            {
                EditorGUILayout.Toggle("Is Schema Initialized?", IsInitialized);
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
                    LatestResponse = SaveManifest(manifestFilePath);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"Selected Scheme: {selectedSchemeName} ({selectedSchemaIndex})");

                GUILayout.Label("Attribute Filters:");
                foreach (var filter in attributeFilters)
                {
                    GUILayout.Label($"{filter.Key}: {filter.Value}");
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
                        Logger.LogWarning($"Scheme '{schemeName}' has deleted {numDeleted} entries.");
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

            // SCHEMA_DEBUG scripting define commands
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            if (defines.Contains("SCHEMA_DEBUG"))
            {
                if (GUILayout.Button("Remove SCHEMA_DEBUG Scripting Define"))
                {
                    var newDefines = string.Join(";", defines.Split(';').Where(d => d != "SCHEMA_DEBUG"));
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
                    Debug.Log("Removed SCHEMA_DEBUG scripting define.");
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
                    Debug.Log("Added SCHEMA_DEBUG scripting define.");
                }
            }
        }
    }
}