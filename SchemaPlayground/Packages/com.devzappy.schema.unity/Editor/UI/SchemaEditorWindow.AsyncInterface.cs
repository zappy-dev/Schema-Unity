using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using UnityEditor;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaContext;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        private async Task OnSchemeChange(SchemaContext ctx, (string DisplayName, string SchemeName, DataScheme Scheme)[] schemeNames, int previousIndex, CancellationToken cancellationToken)
        {
            var nextSelectedSchema = schemeNames[selectedSchemaIndex];
            bool switchSucceeded = await OnSelectScheme(nextSelectedSchema.SchemeName, ctx, cancellationToken);
                            
            // If the switch was canceled, restore the previous selection
            if (!switchSucceeded)
            {
                selectedSchemaIndex = previousIndex;
            }
        }
        
        private async Task<bool> OnSelectScheme(string schemeName, SchemaContext context, CancellationToken cancellationToken = default)
        {
            // Check if any schemes have unsaved changes (excluding the manifest)
            if (!string.IsNullOrEmpty(SelectedSchemeName) && SelectedSchemeName != schemeName)
            {
                var dirtySchemes = GetSchemes(context)
                    .Where(s => s.IsDirty && !s.IsManifest)
                    .ToList();

                if (dirtySchemes.Any())
                {
                    // Build a message listing all dirty schemes
                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine("The following schemes have unsaved changes:");
                    messageBuilder.AppendLine();
                    foreach (var dirtyScheme in dirtySchemes)
                    {
                        messageBuilder.AppendLine($"  • {dirtyScheme.SchemeName}");
                    }
                    messageBuilder.AppendLine();
                    messageBuilder.Append("Do you want to save all changes?");

                    int choice = EditorUtility.DisplayDialogComplex(
                        "Unsaved Changes",
                        messageBuilder.ToString(),
                        "Save All",
                        "Cancel",
                        "Don't Save");

                    if (choice == 0) // Save All
                    {
                        // Save all dirty schemes
                        foreach (var dirtyScheme in dirtySchemes)
                        {
                            var saveResult = await SaveDataScheme(context, dirtyScheme, alsoSaveManifest: false, cancellationToken);
                            await EditorMainThread.Switch(cancellationToken);
                            if (saveResult.Failed)
                            {
                                EditorUtility.DisplayDialog(
                                    "Save Failed",
                                    $"Failed to save scheme '{dirtyScheme.SchemeName}': {saveResult.Message}",
                                    "OK");
                                return false; // Don't switch schemes if any save failed
                            }
                        }
                    }
                    else if (choice == 1) // Cancel
                    {
                        return false; // Don't switch schemes
                    }
                    // choice == 2 means "Don't Save", so we continue without saving
                }
            }

            // Unfocus any selected control fields when selecting a new scheme
            ReleaseControlFocus();

            if (GetAllSchemes(context).Try(out var allSchemes))
            {
                var schemeNames = allSchemes.ToArray();
                var prevSelectedIndex = Array.IndexOf(schemeNames, schemeName);
                if (prevSelectedIndex == -1)
                {
                    return false;
                }
                selectedSchemaIndex = prevSelectedIndex;
            }
            
            LogDbgVerbose($"Opening Schema '{schemeName}' for editing, {context}...");
            SelectedSchemeName = schemeName;
            EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, schemeName);
            newAttributeName = string.Empty;
            
            // Clear virtual scrolling cache when switching schemes
            _virtualTableView?.ClearCache();
            
            return true;
        }

        private async Task<SchemaResult> OnLoadManifest(SchemaContext context, CancellationToken cancellationToken = default)
        {
            using var reporter = new EditorProgressReporter("Schema - Manifest Load");
            LogDbgVerbose("Loading Manifest", context);
            LatestManifestLoadResponse = await LoadManifestFromPath(context, _defaultManifestLoadPath, _defaultUnityProjectPath, reporter, cancellationToken);
            LatestResponse = LatestManifestLoadResponse.Cast();

            if (LatestManifestLoadResponse.Passed)
            {
                await RunManifestMigrationWizard(cancellationToken);
            }
            return LatestResponse;
        }

        private async Task OnSaveScheme(DataScheme schemeToSave, CancellationToken cancellationToken)
        {
            LatestResponse = await SaveDataScheme(EditContext | new SchemaContext
            {
                Scheme = schemeToSave,
                Driver = "User_Save_Scheme",
            }, schemeToSave, alsoSaveManifest: false, cancellationToken);
        }

        private async Task OnExportScheme(DataScheme schemeToExport, ISchemeStorageFormat storageFormat, CancellationToken cancellationToken)
        {
            LatestResponse = await storageFormat.Export(schemeToExport, EditContext | new SchemaContext
            {
                Scheme = schemeToExport,
                Driver = "User_Export_Scheme",
            }, UnityEditorPublishConfig.ResolveExportPath, cancellationToken);
        }

        private async Task OnImportScheme(ISchemeStorageFormat storageFormat, CancellationToken cancellationToken)
        {
            var ctx = EditContext.WithDriver("User_Import_Schema");
            (bool success, var importedSchema, var importFilePath) = await storageFormat.TryImport(ctx, cancellationToken);
            if (success)
            {
                await SubmitAddSchemeRequest(ctx, importedSchema, importFilePath: importFilePath);
            }
        }
    }
}