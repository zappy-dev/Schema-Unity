#define SCHEME_DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Schemes;
using static Schema.Core.SchemaResult;

namespace Schema.Core
{
    public partial class Schema
    {
        public enum ManifestLoadStatus
        {
            FAILED_TO_LOAD_ENTRIES,
            FULLY_LOADED
        }
        
        #region Persistence Operations

        #region Load Operations
        
        
        public static SchemaResult<ManifestLoadStatus> LoadManifestFromPath(string manifestLoadPath,
            SchemaContext context, IProgress<(float, string)> progress = null)
        {
            if (string.IsNullOrWhiteSpace(manifestLoadPath))
            {
                return SchemaResult<ManifestLoadStatus>.Fail("Manifest path is invalid: " + manifestLoadPath, context: context);
            }

            if (!Serialization.Storage.FileSystem.FileExists(manifestLoadPath))
            {
                return SchemaResult<ManifestLoadStatus>.Fail($"No Manifest scheme found.\nSearched the following path: {manifestLoadPath}\nLoad an existing manifest scheme or save the empty template.", context: context);
            }
            
            lock (manifestOperationLock)
            {
                // clear out previous data in case it is stagnant
                var prevDataSchemes = Schema.loadedSchemes;
                
                Logger.LogDbgVerbose($"Schema: Unloading all schemes");
                Schema.loadedSchemes.Clear();
            
                progress?.Report((0f, $"Loading: {manifestLoadPath}..."));
                Logger.Log($"Loading manifest from file: {manifestLoadPath}...", "Manifest");
                var loadResult =
                    Serialization.Storage.DefaultManifestStorageFormat.DeserializeFromFile(manifestLoadPath);
                if (!loadResult.Try( out var manifestDataScheme))
                {
                    return loadResult.CastError<ManifestLoadStatus>();
                }
                
                var loadStopwatch = Stopwatch.StartNew();
                int currentSchema = 0;
                int schemeCount = manifestDataScheme.EntryCount;

                nextManifestScheme = new ManifestScheme(manifestDataScheme);
                var saveManifestResponse = LoadDataScheme(manifestDataScheme, overwriteExisting: true,
                    registerManifestEntry: false);
                Logger.LogDbgVerbose(saveManifestResponse.Message, saveManifestResponse.Context);
                if (!saveManifestResponse.Passed)
                {
                    Logger.LogError(saveManifestResponse.Message, saveManifestResponse.Context);
                    nextManifestScheme = null;
                    return SchemaResult<ManifestLoadStatus>.Fail(saveManifestResponse.Message, context: Context.Manifest);
                }
                
                Logger.Log($"Loaded {manifestDataScheme}, scheme count: {schemeCount}", manifestDataScheme);

                var sb = new StringBuilder();
                bool success = true;
                // sanitize data
                var schemaGroups = manifestDataScheme.AllEntries.GroupBy(e => e.GetData(nameof(ManifestEntry.SchemeName)));
                var schemaGroupsArray = schemaGroups as IGrouping<object, DataEntry>[] ?? schemaGroups.ToArray();
                foreach (var duplicateEntriesGroup in schemaGroupsArray.Where(g => g.Count() > 1))
                {
                    sb.AppendLine($"Found {duplicateEntriesGroup.Count()} manifest entries for scheme '{duplicateEntriesGroup.Key}'");
                }
                
                // 1. Load schemes into memory
                var progressWrapper = new Progress<string>(schemeFilePath =>
                {
                    progress?.Report((currentSchema * 1.0f / schemeCount,
                        $"Loading ({currentSchema}/{schemeCount}): {schemeFilePath}"));
                });
                var loadedSchemes = new List<DataScheme>();
                foreach (var manifestEntry in nextManifestScheme.GetEntries())
                {
                    currentSchema++;
                    Logger.Log($"Loading manifest entry {manifestEntry._}...", manifestDataScheme);

                    if (manifestEntry.SchemeName.Equals(Manifest.MANIFEST_SCHEME_NAME))
                    {
                        var manifestFilePath = manifestEntry.FilePath;
                        if (string.IsNullOrWhiteSpace(manifestFilePath))
                        {
                            // skip empty manifest self entry, this is allowed, especially for empty / not-yet persisted projects.
                            continue;
                        }
                    }
                    
                    var loadSchemeRes = LoadSchemeFromManifestEntry(manifestEntry,
                        progress: progressWrapper);
                    if (!loadSchemeRes.Try(out var loadedScheme))
                    {
                        success = false;
                        Logger.LogError(loadSchemeRes.Message, loadSchemeRes.Context);
                        sb.AppendLine(loadSchemeRes.Message);
                        continue;
                    }

                    // allow loaded manifest to overwrite in-memory manifest
                    bool isSelfEntry = loadedScheme.IsManifest;
                    loadedScheme.IsDirty = false; // is the loaded scheme dirty? type conversions?
                    if (isSelfEntry)
                    {
                        if (!manifestDataScheme.Equals(loadedScheme))
                        {
                            // TODO: Clarify this message better
                            Logger.LogError($"Mismatch between loaded manifest scheme and manifest scheme referenced by loaded manifest.", context: manifestDataScheme);
                        }

                        continue;
                        // TODO: How best to handle loading the manifest schema while already loading the manifest schema?
                        // Add validation to make sure it is the same file path?
                        // continue;
                    }
                    
                    loadedSchemes.Add(loadedScheme);
                }
                
                // TODO: topological sort so schemes are loaded in reference order
                var schemeLoadOrder = loadedSchemes.OrderBy(scheme => scheme.GetReferenceAttributes().Count() );

                progress?.Report((0.1f, "Loading..."));
                foreach (var scheme in schemeLoadOrder)
                {
                    Logger.LogDbgVerbose($"Loading scheme from manifest: {scheme}", context: manifestDataScheme);
                    var loadRes = LoadDataScheme(scheme,
                        overwriteExisting: false,
                        registerManifestEntry: false);
                    if (loadRes.Failed)
                    {
                        success = false;
                        Logger.LogError(loadRes.Message, loadRes.Context);
                        sb.AppendLine(loadRes.Message);
                    }
                    
                    Logger.LogDbgVerbose(loadRes.Message, loadRes.Context);
                }

                if (!success)
                {
                    loadedManifestScheme = nextManifestScheme;
                    manifestImportPath = manifestLoadPath;
                    nextManifestScheme = null;
                    return SchemaResult<ManifestLoadStatus>.Pass(ManifestLoadStatus.FAILED_TO_LOAD_ENTRIES, $"Failed to load all schemes found in manifest from {manifestLoadPath}, errors: {sb}", context: "Manifest");
                }
                
                // overwrite existing manifest
                loadStopwatch.Stop();

                loadedManifestScheme = nextManifestScheme;
                nextManifestScheme = null;
                return SchemaResult<ManifestLoadStatus>.Pass(ManifestLoadStatus.FULLY_LOADED, $"Loaded {schemeCount} schemes from manifest in {loadStopwatch.ElapsedMilliseconds:N0} ms", context: "Manifest");
            }
        }
        
        internal static SchemaResult<DataScheme> LoadSchemeFromManifestEntry(ManifestEntry manifestEntry,
            IProgress<string> progress = null)
        {
            if (manifestEntry == null)
            {
                return SchemaResult<DataScheme>.Fail($"Failed to load manifest from {manifestEntry}", Context.Manifest);
            }
            
            // valid manifest entries
            var entrySchemaName = manifestEntry.SchemeName;

            if (string.IsNullOrWhiteSpace(entrySchemaName))
            {
                return SchemaResult<DataScheme>.Fail($"Failed to parse manifest entry '{manifestEntry}'", Context.Manifest);
            }
                    
            var schemeFilePath = manifestEntry.FilePath;

            if (string.IsNullOrWhiteSpace(schemeFilePath))
            {
                return SchemaResult<DataScheme>.Fail($"{manifestEntry} Invalid scheme file path: {schemeFilePath}",
                    Context.Manifest);
            }
            
            // Resolve relative path if needed
            string resolvedPath = schemeFilePath;
            if (!IO.PathUtility.IsAbsolutePath(schemeFilePath) && !string.IsNullOrEmpty(ManifestImportPath))
            {
                resolvedPath = IO.PathUtility.MakeAbsolutePath(schemeFilePath, ContentLoadPath);
            }
            
            if (!Serialization.Storage.FileSystem.FileExists(resolvedPath))
            {
                return SchemaResult<DataScheme>.Fail($"{manifestEntry} Scheme file not found: {resolvedPath} (from {schemeFilePath})",
                    Context.Manifest);
            }
                    
            progress?.Report(resolvedPath);
                
            // TODO support async loading
            if (!Serialization.Storage.DefaultSchemaStorageFormat.DeserializeFromFile(resolvedPath)
                    .Try(out var loadedSchema))
            {
                return SchemaResult<DataScheme>.Fail($"Failed to load scheme from file: {resolvedPath}", context: Context.Manifest);
            }
            
            return SchemaResult<DataScheme>.Pass(loadedSchema, $"Loaded scheme data from file", Context.Manifest);
        }

        /// <summary>
        /// Load a new scheme into memory.
        /// Note: This does not persist the new data scheme to disk
        /// </summary>
        /// <param name="scheme">New scheme to load</param>
        /// <param name="overwriteExisting">If true, overwrites an existing scheme. If false, fails to overwrite an existing scheme if it exists</param>
        /// <param name="registerManifestEntry">If true, registers a new manifest entry in the currently loaded manifest for the given scheme if loaded.</param>
        /// <param name="importFilePath">File path from where this scheme was imported, if imported</param>
        /// <returns></returns>
        public static SchemaResult LoadDataScheme(DataScheme scheme, bool overwriteExisting, bool registerManifestEntry = true, string importFilePath = null)
        {
            string schemeName = scheme.SchemeName;
            
            // input validation
            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return Fail("Schema name is invalid: " + schemeName, context: scheme);
            }
            
            if (loadedSchemes.ContainsKey(schemeName) && !overwriteExisting)
            {
                return Fail("Schema already exists: " + schemeName, context: scheme);
            }

            // TODO: This can be parallelized
            // process all incoming entry data and make sure it is in a valid formats 
            foreach (var entry in scheme.AllEntries)
            {
                foreach (var attribute in scheme.GetAttributes())
                {
                    attribute._scheme = scheme;
                    
                    var entryData = entry.GetData(attribute);
                    if (entryData.Failed)
                    {
                        // Don't have the data for this attribute, set to default value
                        scheme.SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue());
                    }
                    else
                    {
                        var fieldData = entryData.Result;
                        var validateData = attribute.CheckIfValidData(fieldData);
                        if (validateData.Failed) // Try to force the manifest to be loaded, TODO: better handli
                        {
                            var conversion = attribute.ConvertData(fieldData);
                            if (conversion.Failed)
                            {
                                // TODO: What should the user flow be here? Auto-convert? Prompt for user feedback?
                                // tried to convert the data, failed
                                
                                // Allow file path data types to load in, even if the file doesn't exist.
                                // TODO: Runtime warn users when a filepath doesn't exist
                                // var allowFailedConversion = attribute.DataType == DataType.FilePath ||
                                //                             attribute.DataType is ReferenceDataType;
                                //
                                // if (!allowFailedConversion)
                                // {
                                    return Fail($"{scheme}.{attribute}: {conversion.Message}", context: scheme);
                                // }
                            }

                            var updateData = scheme.SetDataOnEntry(entry, attribute.AttributeName, conversion.Result, allowIdentifierUpdate: true);
                            if (updateData.Failed)
                            {
                                return Fail(updateData.Message, scheme);
                            }
                        }
                    }
                }
            }
        
            Logger.LogDbgVerbose($"Schema: Loading scheme {scheme.SchemeName}");
            loadedSchemes[schemeName] = scheme;
            if (scheme.IsManifest)
            {
                IsTemplateManifestLoaded = false;
            }

            if (registerManifestEntry)
            {
                // add new manifest entry if not existing. Only do this for non-manifest schemes, else this could end up in an stack overflow
                if (scheme.IsManifest || GetManifestEntryForScheme(schemeName).Try(out _)) 
                    return Pass("Schema added", scheme);
                
                // Add a new entry to the manifest for this loaded scheme
                lock (manifestOperationLock)
                {
                    if (!GetManifestEntryForScheme(schemeName).Try(out _))
                    {
                        // add manifest record for new scheme
                    
                        Logger.Log($"Adding manifest entry for {schemeName} with import path: {importFilePath}...", Context.Manifest);
                        var manifest = GetManifestScheme();
                        if (!manifest.Try(out var manifestScheme))
                        {
                            return Fail(manifest.Message);
                        }

                        return manifestScheme.AddManifestEntry(schemeName, importFilePath).Cast();
                    }
                }
            }

            return Pass("Schema added", scheme);
        }

        public static SchemaResult UnloadScheme(string schemeName)
        {
            Logger.LogDbgWarning($"Unloading Scheme: {schemeName}");
            bool wasRemoved = loadedSchemes.Remove(schemeName);

            return CheckIf(wasRemoved, "Scheme was not unloaded", context: Context.System, successMessage: "Scheme was unloaded.");
        }
        #endregion

        #region Save Operations
        public static SchemaResult SaveManifest(IProgress<float> progress = null)
        {
            Logger.Log($"Saving manifest to file: {ManifestImportPath}...", "Manifest");
            if (string.IsNullOrWhiteSpace(ManifestImportPath))
            {
                return Fail("Manifest path is invalid: " + ManifestImportPath, "Manifest");
            }
            
            progress?.Report(0f);
            lock (manifestOperationLock)
            {
                var previousManifestPath = ManifestSelfEntry.FilePath;
                try
                {
                    var manifestRelativeLoadPath = PathUtility.MakeRelativePath(ManifestImportPath, ContentLoadPath);
                    ManifestSelfEntry.FilePath = manifestRelativeLoadPath;

                    if (Serialization.Storage.DefaultManifestStorageFormat.SerializeToFile(ManifestImportPath, GetManifestScheme().Result._).Passed)
                    {
                        GetManifestScheme().Result.IsDirty = false;
                    }
                }
                catch (Exception ex)
                {
                    // rollback bad path
                    ManifestSelfEntry.FilePath = previousManifestPath;
                    Logger.LogError(ex.ToString());
                    return Fail($"Failed to save manifest to {ManifestImportPath}\n{ex.Message}");
                }
            }
            
            return Pass($"Saved manifest to path: {ManifestImportPath}");
        }

        public static SchemaResult Save(bool saveManifest = false)
        {
            foreach (var scheme in GetSchemes())
            {
                if (scheme.IsDirty)
                {
                    if (scheme.IsManifest && !saveManifest)
                    {
                        continue;
                    }
                    
                    var result = SaveDataScheme(scheme, saveManifest);
                    if (result.Failed)
                    {
                        return result;
                    }
                }
            }
            
            return Pass("Saved all dirty schemes");
        }

        public static SchemaResult SaveDataScheme(DataScheme scheme, bool alsoSaveManifest)
        {
            Logger.Log($"Saving {scheme} to file...", "Storage");
            if (scheme == null)
            {
                return Fail("Attempted to save an invalid Data scheme");
            }
            
            var saveStopwatch = Stopwatch.StartNew();
            if (!GetManifestEntryForScheme(scheme.SchemeName).Try(out var schemeManifestEntry))
            {
                return Fail("Could not find manifest entry for scheme name: " + scheme.SchemeName);
            }
            
            if (scheme.IsManifest && GetManifestScheme().Try(out var loadedManifestScheme) &&
                !loadedManifestScheme._.Equals(scheme))
            {
                var sb = new StringBuilder();
                sb.Append(
                    "Attempting to save a Manifest scheme that is different from the loaded Manifest! This can lead to inconsistent behaviour");
                #if SCHEME_DEBUG
                sb.AppendLine("Loaded Manifest Scheme");
                loadedManifestScheme._.Write(sb, true);
                sb.AppendLine("Saving Manifest Scheme");
                scheme.Write(sb, true);
                #endif
                return Fail(
                    sb.ToString());
            }

            // Only serialize the manifest to disk once
            string savePath = ""; 
            SchemaResult result = NoOp;
            if (!scheme.IsManifest)
            {
                // Get the file path from the manifest entry
                savePath = schemeManifestEntry.FilePath;
                
                // Resolve relative path if needed
                string resolvedPath = savePath;
                if (!PathUtility.IsAbsolutePath(savePath) && !string.IsNullOrEmpty(ManifestImportPath))
                {
                    resolvedPath = PathUtility.MakeAbsolutePath(savePath, ContentLoadPath);
                    
                    // Ensure the directory exists
                    string directory = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrEmpty(directory) && !Serialization.Storage.FileSystem.DirectoryExists(directory))
                    {
                        Serialization.Storage.FileSystem.CreateDirectory(directory);
                    }
                }
                
                Logger.Log($"Saving {scheme} to file {resolvedPath} (from path {savePath})", "Storage");
                result = Serialization.Storage.DefaultSchemaStorageFormat.SerializeToFile(resolvedPath, scheme);
            }

            if (scheme.IsManifest || alsoSaveManifest)
            {
                ManifestScheme manifestScheme;
                if (alsoSaveManifest)
                {
                    // use loaded manifest schema to update entry
                    var loadedManifestRes = GetManifestScheme();
                    if (!loadedManifestRes.Try(out manifestScheme))
                    {
                        return loadedManifestRes.Cast();
                    }
                }
                else
                {
                    // Try given scheme as a manifest to update self entry
                    manifestScheme = new ManifestScheme(scheme);
                }
                var manifestSelfEntry = manifestScheme.SelfEntry;
                
                if (string.IsNullOrWhiteSpace(manifestSelfEntry.FilePath))
                {
                    manifestSelfEntry.FilePath = Path.GetFileName(ManifestImportPath);
                }
                
                savePath = ManifestImportPath;
                Logger.Log($"Saving manifest {scheme} to file {ManifestImportPath}", "Storage");
                result = SaveManifest();
            }
            saveStopwatch.Stop();

            if (result.Passed)
            {
                Logger.Log($"Saved {scheme} to file {savePath} in {saveStopwatch.ElapsedMilliseconds:N0} ms", context: "Storage");
                scheme.IsDirty = false;
            }
            return result;
        }
        
        #endregion
        
        #endregion
    }
}