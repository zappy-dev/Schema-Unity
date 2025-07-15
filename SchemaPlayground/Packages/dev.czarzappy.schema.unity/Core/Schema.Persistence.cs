using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Schema.Core.Data;
using Schema.Core.Serialization;
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
        
        
        public static SchemaResult<ManifestLoadStatus> LoadManifestFromPath(string manifestPath, IProgress<(float, string)> progress = null)
        {
            Logger.Log($"Loading manifest from file: {manifestPath}...", "Manifest");
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return SchemaResult<ManifestLoadStatus>.Fail("Manifest path is invalid: " + manifestPath, context: "Manifest");
            }

            if (!Serialization.Storage.FileSystem.FileExists(manifestPath))
            {
                return SchemaResult<ManifestLoadStatus>.Fail($"No Manifest scheme found.\nSearched the following path: {manifestPath}\nLoad an existing manifest scheme or save the empty template.", context: "Manifest");
            }
            
            lock (manifestOperationLock)
            {
                // clear out previous data in case it is stagnant
                var prevDataSchemes = dataSchemes;
                dataSchemes.Clear();
            
                progress?.Report((0f, $"Loading: {manifestPath}..."));
                Logger.Log($"Loading manifest from file: {manifestPath}...", "Manifest");
                if (!Serialization.Storage.DefaultManifestStorageFormat.DeserializeFromFile(manifestPath)
                        .Try( out var loadedManifestSchema))
                {
                    return SchemaResult<ManifestLoadStatus>.Fail("Failed to load manifest schema.", context: "Manifest");
                }
                
                var loadStopwatch = Stopwatch.StartNew();
                int currentSchema = 0;
                int schemeCount = loadedManifestSchema.EntryCount;
                
                var saveManifestResponse = LoadDataScheme(loadedManifestSchema, overwriteExisting: true,
                    updateManifest: false);
                if (!saveManifestResponse.Passed)
                {
                    return SchemaResult<ManifestLoadStatus>.Fail(saveManifestResponse.Message, context: Context.Manifest);
                }
                
                Logger.Log($"Loaded {loadedManifestSchema}, scheme count: {schemeCount}", "Manifest");

                var sb = new StringBuilder();
                bool success = true;
                // sanitize data
                var schemaGroups = loadedManifestSchema.AllEntries.GroupBy(e => e.GetData(MANIFEST_ATTRIBUTE_SCHEME_NAME));
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
                foreach (var manifestEntry in schemaGroupsArray.Select(g => g.First()))
                {
                    currentSchema++;
                    Logger.Log($"Handling entry {manifestEntry}...", "Manifest");

                    if (manifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME).Equals(MANIFEST_SCHEME_NAME))
                    {
                        var manifestFilePath = manifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
                        if (string.IsNullOrWhiteSpace(manifestFilePath))
                        {
                            // skip empty manifest self entry, this is allowed, especially for empty / not-yet persisted projects.
                            continue;
                        }
                    }
                    
                    var loadScheme = LoadSchemeFromManifestEntry(manifestEntry,
                        progress: progressWrapper);
                    if (!loadScheme.Try(out var loadedScheme))
                    {
                        success = false;
                        sb.AppendLine(loadScheme.Message);
                        continue;
                    }

                    // allow loaded manifest to overwrite in-memory manifest
                    bool isSelfEntry = loadedScheme.IsManifest;
                    loadedScheme.IsDirty = false; // is the loaded scheme dirty? type conversions?
                    if (isSelfEntry)
                    {
                        if (!loadedManifestSchema.Equals(loadedScheme))
                        {
                            // TODO: Clarify this message better
                            Logger.LogError($"Mismatch between loaded manifest scheme and manifest scheme referenced by loaded manifest.");
                        }
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
                    var loadScheme = LoadDataScheme(scheme,
                        overwriteExisting: false,
                        updateManifest: false);
                    if (loadScheme.Failed)
                    {
                        success = false;
                        sb.AppendLine(loadScheme.Message);
                    }
                }

                if (!success)
                {
                    return SchemaResult<ManifestLoadStatus>.Pass(ManifestLoadStatus.FAILED_TO_LOAD_ENTRIES, $"Failed to load all schemes found in manifest from {manifestPath}, errors: {sb}", context: "Manifest");
                }
                
                // overwrite existing manifest
                loadStopwatch.Stop();
            
                return SchemaResult<ManifestLoadStatus>.Pass(ManifestLoadStatus.FULLY_LOADED, $"Loaded {schemeCount} schemes from manifest in {loadStopwatch.ElapsedMilliseconds:N0} ms", context: "Manifest");
            }
        }
        
        internal static SchemaResult<DataScheme> LoadSchemeFromManifestEntry(DataEntry manifestEntry,
            IProgress<string> progress = null)
        {
            if (manifestEntry == null)
            {
                return SchemaResult<DataScheme>.Fail($"Failed to load manifest from {manifestEntry}", Context.Manifest);
            }
            
            // valid manifest entries
            if (!manifestEntry.TryGetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME, out var entrySchemaName))
            {
                return SchemaResult<DataScheme>.Fail($"Failed to parse manifest entry '{manifestEntry}'", Context.Manifest);
            }

            if (string.IsNullOrWhiteSpace(entrySchemaName))
            {
                return SchemaResult<DataScheme>.Fail($"Failed to parse manifest entry '{manifestEntry}'", Context.Manifest);
            }
                    
            if (!manifestEntry.TryGetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH, out var schemeFilePath))
            {
                // TODO: Report partial load failure
                return SchemaResult<DataScheme>.Fail($"No attribute data for '{MANIFEST_ATTRIBUTE_SCHEME_NAME}' in {manifestEntry}",
                    Context.Manifest);
            }

            if (string.IsNullOrWhiteSpace(schemeFilePath) || !Serialization.Storage.FileSystem.FileExists(schemeFilePath))
            {
                return SchemaResult<DataScheme>.Fail($"{manifestEntry} Invalid scheme file path: {schemeFilePath}",
                    Context.Manifest);
            }
                    
            progress?.Report(schemeFilePath);
                
            // TODO support async loading
            if (!Serialization.Storage.DefaultSchemaStorageFormat.DeserializeFromFile(schemeFilePath)
                    .Try(out var loadedSchema))
            {
                return SchemaResult<DataScheme>.Fail("Failed to load scheme from file.", context: Context.Manifest);
            }
            
            return SchemaResult<DataScheme>.Pass(loadedSchema, $"Loaded scheme data from file", Context.Manifest);
        }
        
        
        
        /// <summary>
        /// Load a new scheme into memory.
        /// Note: This does not persist the new data scheme to disk
        /// </summary>
        /// <param name="scheme">New scheme to load</param>
        /// <param name="overwriteExisting">If true, overwrites an existing scheme. If false, fails to overwrite an existing scheme if it exists</param>
        /// <param name="importFilePath">File path from where this scheme was imported, if imported</param>
        /// <returns></returns>
        public static SchemaResult LoadDataScheme(DataScheme scheme, bool overwriteExisting, bool updateManifest = true, string importFilePath = null)
        {
            Logger.Log($"Adding {scheme}...", "System");
            string schemeName = scheme.SchemeName;
            
            // input validation
            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return Fail("Schema name is invalid: " + schemeName, scheme);
            }
            
            if (dataSchemes.ContainsKey(schemeName) && !overwriteExisting)
            {
                return Fail("Schema already exists: " + schemeName);
            }

            // process all incoming entry data and make sure it is in a valid formats 
            foreach (var entry in scheme.AllEntries)
            {
                foreach (var attribute in scheme.GetAttributes())
                {
                    var entryData = entry.GetData(attribute);
                    if (entryData.Failed)
                    {
                        // Don't have the data for this attribute, set to default value
                        scheme.SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue());
                    }
                    else
                    {
                        var fieldData = entryData.Result;
                        var validateData = attribute.DataType.CheckIfValidData(fieldData);
                        if (validateData.Failed && !scheme.IsManifest) // Try to force the manifest to be loaded, TODO: better handli
                        {
                            var conversion = attribute.DataType.ConvertData(fieldData);
                            if (conversion.Failed)
                            {
                                // TODO: What should the user flow be here? Auto-convert? Prompt for user feedback?
                                // tried to convert the data, failed
                                
                                // Allow file path data types to load in, even if the file doesn't exist.
                                // TODO: Runtime warn users when a filepath doesn't exist
                                return CheckIf(attribute.DataType == DataType.FilePath, conversion.Message,
                                    context: scheme);
                            }

                            var updateData = scheme.SetDataOnEntry(entry, attribute.AttributeName, conversion.Result);
                            if (updateData.Failed)
                            {
                                return Fail(updateData.Message, scheme);
                            }
                        }
                    }
                }
            }
        
            dataSchemes[schemeName] = scheme;

            if (updateManifest)
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
                        var newSchemeManifestEntry = new DataEntry();
                        newSchemeManifestEntry.SetData(MANIFEST_ATTRIBUTE_SCHEME_NAME, schemeName);
                    
                        if (!string.IsNullOrWhiteSpace(importFilePath))
                        {
                            // TODO: Record import path or give option to clone / copy file to new content path?
                            newSchemeManifestEntry.SetData(MANIFEST_ATTRIBUTE_FILEPATH, importFilePath);
                        }
                    
                        Logger.Log($"Adding manifest entry for {schemeName} with import path: {importFilePath}...", Context.Manifest);
                        var manifest = GetManifestScheme();
                        if (!manifest.Try(out var manifestScheme))
                        {
                            return Fail(manifest.Message);
                        }
                        
                        return manifestScheme.AddEntry(newSchemeManifestEntry, runDataValidation: false);
                    }
                }
            }

            return Pass("Schema added", scheme);
        }
        
        #endregion

        #region Save Operations
        public static SchemaResult SaveManifest(string manifestPath, IProgress<float> progress = null)
        {
            Logger.Log($"Saving manifest to file: {manifestPath}...", "Manifest");
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return Fail("Manifest path is invalid: " + manifestPath, "Manifest");
            }
            
            progress?.Report(0f);
            lock (manifestOperationLock)
            {
                var previousManifestPath = ManifestSelfEntry.GetData(MANIFEST_ATTRIBUTE_FILEPATH);
                try
                {
                    ManifestSelfEntry.SetData(MANIFEST_ATTRIBUTE_FILEPATH, manifestPath);

                    if (Serialization.Storage.DefaultManifestStorageFormat.SerializeToFile(manifestPath, GetManifestScheme().Result).Passed)
                    {
                        GetManifestScheme().Result.IsDirty = false;
                    }
                }
                catch (Exception ex)
                {
                    ManifestSelfEntry.SetData(MANIFEST_ATTRIBUTE_FILEPATH, previousManifestPath);
                    Logger.LogError(ex.ToString());
                    return Fail($"Failed to save manifest to {manifestPath}\n{ex.Message}");
                }
            }
            
            return Pass($"Saved manifest to path: {manifestPath}");
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
            bool isManifestScheme = scheme.IsManifest;
            if (!GetManifestEntryForScheme(scheme.SchemeName).Try(out var schemeManifestEntry))
            {
                return Fail("Could not find manifest entry for scheme name: " + scheme.SchemeName);
            }

            // Only serialize the manifest to disk once
            string savePath = ""; 
            SchemaResult result = NoOp;
            if (!isManifestScheme)
            {
                // TODO: Handle if the data doesn't yet have a save path from the manifest
                savePath = schemeManifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
                Logger.Log($"Saving {scheme} to file {savePath}", "Storage");
                result = Serialization.Storage.DefaultSchemaStorageFormat.SerializeToFile(savePath, scheme);
            }

            if (isManifestScheme || alsoSaveManifest)
            {
                savePath = ManifestLoadPath;
                Logger.Log($"Saving manifest {scheme} to file {ManifestLoadPath}", "Storage");
                result = SaveManifest(ManifestLoadPath);
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