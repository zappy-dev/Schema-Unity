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

        private static Storage _storage;
        public static void SetStorage(Storage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Retrieves the Storage interface for serialization and IO operations
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static SchemaResult<Storage> GetStorage(SchemaContext context)
        {
            var res = SchemaResult<Storage>.New(context);
            
            if (_storage == null)
            {
                    
                return res.Fail($"No schema storage available. Use {nameof(Schema)}.{nameof(SetStorage)} to initialize the Storage system.");
            }

            return res.Pass(_storage);
        }
        
        #region Persistence Operations

        #region Load Operations
        
        
        public static SchemaResult<ManifestLoadStatus> LoadManifestFromPath(SchemaContext context,
            string manifestLoadPath,
            IProgress<(float, string)> progress = null)
        {
            var res = SchemaResult<ManifestLoadStatus>.New(context);
            if (string.IsNullOrWhiteSpace(manifestLoadPath))
            {
                return res.Fail("Manifest path is invalid: " + manifestLoadPath);
            }

            if (!PathUtility.IsAbsolutePath(manifestLoadPath))
            {
                manifestLoadPath = PathUtility.MakeAbsolutePath(ProjectPath, manifestLoadPath);
                Logger.LogVerbose($"Converting manifest path to absolute path: {manifestLoadPath}");
            }

            if (!GetStorage(context).Try(out var storage, out var storageErr))
            {
                return storageErr.CastError<ManifestLoadStatus>();
            }

            if (storage.FileSystem.FileExists(context, manifestLoadPath).Failed)
            {
                return res.Fail($"No Manifest scheme found.\nSearched the following path: {manifestLoadPath}\nLoad an existing manifest scheme or save the empty template.");
            }
            
            progress?.Report((0f, $"Loading: {manifestLoadPath}..."));
            Logger.LogDbgVerbose($"Loading manifest from file: {manifestLoadPath}...", "Manifest");
            var loadResult =
                _storage.DefaultManifestStorageFormat.DeserializeFromFile(context, manifestLoadPath);
            if (!loadResult.Try( out var manifestDataScheme))
            {
                return loadResult.CastError<ManifestLoadStatus>();
            }

            var loadManifestRes = LoadManifest(context, manifestDataScheme, progress);

            if (loadManifestRes.Passed)
            {
                // Update manifest import path to support Edit-mode validation
                manifestImportPath = manifestLoadPath;
            }
            
            return loadManifestRes;
        }
        
        public static SchemaResult<ManifestLoadStatus> LoadManifest(SchemaContext context,
            DataScheme manifestDataScheme,
            IProgress<(float, string)> progress = null)
        {
            lock (manifestOperationLock)
            {
                // clear out previous data in case it is stagnant
                Logger.LogDbgVerbose($"Schema: Unloading all schemes");
                Schema.loadedSchemes.Clear();
                
                var loadStopwatch = Stopwatch.StartNew();
                int currentSchema = 0;
                int schemeCount = manifestDataScheme.EntryCount;

                nextManifestScheme = new ManifestScheme(manifestDataScheme);
                var saveManifestResponse = LoadDataScheme(context, manifestDataScheme, overwriteExisting: true,
                    registerManifestEntry: false);
                Logger.LogDbgVerbose(saveManifestResponse.Message, saveManifestResponse.Context);
                if (!saveManifestResponse.Passed)
                {
                    Logger.LogError(saveManifestResponse.Message, saveManifestResponse.Context);
                    nextManifestScheme = null;
                    return SchemaResult<ManifestLoadStatus>.Fail(saveManifestResponse.Message, context: context);
                }
                
                Logger.LogDbgVerbose($"Loaded {manifestDataScheme}, scheme count: {schemeCount}", manifestDataScheme);

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
                if (!nextManifestScheme.GetEntries(context).Try(out var entries, out var error))
                    return error.CastError<ManifestLoadStatus>();
                
                foreach (var manifestEntry in entries)
                {
                    currentSchema++;
                    Logger.LogDbgVerbose($"Loading manifest entry {manifestEntry._}...", manifestDataScheme);

                    if (manifestEntry.SchemeName.Equals(Manifest.MANIFEST_SCHEME_NAME))
                    {
                        var manifestFilePath = manifestEntry.FilePath;
                        if (string.IsNullOrWhiteSpace(manifestFilePath))
                        {
                            // skip empty manifest self entry, this is allowed, especially for empty / not-yet persisted projects.
                            continue;
                        }
                    }
                    
                    var loadSchemeRes = LoadSchemeFromManifestEntry(context, manifestEntry,
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
                    loadedScheme.SetDirty(context, false); // is the loaded scheme dirty? type conversions?
                    if (isSelfEntry)
                    {
                        // if (!manifestDataScheme.Equals(loadedScheme))
                        // {
                        //     // TODO: Clarify this message better
                        //     Logger.LogError($"Mismatch between loaded manifest scheme and manifest scheme referenced by loaded manifest.", context: manifestDataScheme);
                        // }

                        continue;
                        // TODO: How best to handle loading the manifest schema while already loading the manifest schema?
                        // Add validation to make sure it is the same file path?
                        // continue;
                    }
                    
                    loadedSchemes.Add(loadedScheme);
                }
                
                if (!DataScheme.TopologicalSortByReferences(context, loadedSchemes).Try(out var schemeLoadOrder, out var sortErr)) 
                    return sortErr.CastError<ManifestLoadStatus>();

                progress?.Report((0.1f, "Loading..."));
                foreach (var scheme in schemeLoadOrder)
                {
                    Logger.LogDbgVerbose($"Loading scheme from manifest: {scheme}", context: manifestDataScheme);
                    var loadRes = LoadDataScheme(context, scheme,
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
                    LoadedManifestScheme = nextManifestScheme;
                    nextManifestScheme = null;
                    return SchemaResult<ManifestLoadStatus>.Pass(ManifestLoadStatus.FAILED_TO_LOAD_ENTRIES, $"Failed to load all schemes found in manifest, errors: {sb}", context: "Manifest");
                }
                
                // overwrite existing manifest
                loadStopwatch.Stop();

                LoadedManifestScheme = nextManifestScheme;
                nextManifestScheme = null;
                return SchemaResult<ManifestLoadStatus>.Pass(ManifestLoadStatus.FULLY_LOADED, $"Loaded {schemeCount} schemes from manifest in {loadStopwatch.ElapsedMilliseconds:N0} ms", context: "Manifest");
            }
        }
        
        internal static SchemaResult<DataScheme> LoadSchemeFromManifestEntry(SchemaContext context, ManifestEntry manifestEntry,
            IProgress<string> progress = null)
        {
            var res = SchemaResult<DataScheme>.New(context);
            if (manifestEntry == null)
            {
                return res.Fail($"Failed to load manifest from {manifestEntry}");
            }
            
            // valid manifest entries
            var entrySchemaName = manifestEntry.SchemeName;

            if (string.IsNullOrWhiteSpace(entrySchemaName))
            {
                return res.Fail($"Failed to parse manifest entry '{manifestEntry}'");
            }
                    
            var schemeFilePath = manifestEntry.FilePath;

            if (string.IsNullOrWhiteSpace(schemeFilePath))
            {
                return res.Fail($"{manifestEntry} Invalid scheme file path: {schemeFilePath}");
            }
            
            // Resolve relative path if needed
            // TODO: Unify logic to FS Data Type
            string resolvedPath = schemeFilePath;
            if (!PathUtility.IsAbsolutePath(schemeFilePath) && !string.IsNullOrEmpty(ManifestImportPath))
            {
                resolvedPath = PathUtility.MakeAbsolutePath(schemeFilePath, ProjectPath);
            }

            var fileExistRes = _storage.FileSystem.FileExists(context, resolvedPath);
            if (fileExistRes.Failed)
            {
                return fileExistRes.CastError<DataScheme>();
            }
                    
            progress?.Report(resolvedPath);
                
            // TODO support async loading
            if (!_storage.DefaultSchemeStorageFormat.DeserializeFromFile(context, resolvedPath)
                    .Try(out var loadedSchema, out var loadErr))
            {
                return res.Fail($"Failed to load scheme from file: {resolvedPath}, reason: {loadErr.Message}");
            }
            
            return res.Pass(loadedSchema, $"Loaded scheme data from file");
        }

        /// <summary>
        /// Load a new scheme into memory.
        /// Note: This does not persist the new data scheme to disk
        /// </summary>
        /// <param name="context"></param>
        /// <param name="scheme">New scheme to load</param>
        /// <param name="overwriteExisting">If true, overwrites an existing scheme. If false, fails to overwrite an existing scheme if it exists</param>
        /// <param name="registerManifestEntry">If true, registers a new manifest entry in the currently loaded manifest for the given scheme if loaded.</param>
        /// <param name="importFilePath">File path from where this scheme was imported, if imported</param>
        /// <returns></returns>
        public static SchemaResult LoadDataScheme(SchemaContext context, DataScheme scheme, bool overwriteExisting, bool registerManifestEntry = true, string importFilePath = null)
        {
            using var schemeScope = new SchemeContextScope(ref context, scheme);
            string schemeName = scheme.SchemeName;
            
            // input validation
            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return Fail(context, errorMessage: "Schema name is invalid: " + schemeName);
            }
            
            if (loadedSchemes.ContainsKey(schemeName) && !overwriteExisting)
            {
                return Fail(context, errorMessage: "Schema already exists: " + schemeName);
            }

            var schemeAttributes = scheme.GetAttributes().ToList();
            // TODO: This can be parallelized
            // process all incoming entry data and make sure it is in a valid formats 
            foreach (var entry in scheme.AllEntries)
            {
                var attributesToRemove = new List<string>();
                // prune unknown attributes
                using var entryEnumerator = entry.GetEnumerator();
                while (entryEnumerator.MoveNext())
                {
                    var kvp = entryEnumerator.Current;
                    var attrName = kvp.Key;
                    bool isKnownAttribute = schemeAttributes.Select(attr => attr.AttributeName).Contains(attrName);
                    if (isKnownAttribute)
                    {
                        continue;
                    }
                    
                    attributesToRemove.Add(attrName);
                }

                foreach (var attrName in attributesToRemove)
                {
                    using var _ = new AttributeContextScope(ref context, attrName);
                    var removeRes = entry.RemoveData(context, attrName);
                    if (removeRes.Failed)
                    {
                        return removeRes;
                    }
                }
                
                foreach (var attribute in schemeAttributes)
                {
                    attribute._scheme = scheme;
                    
                    var entryData = entry.GetData(attribute);
                    if (entryData.Failed)
                    {
                        // Don't have the data for this attribute, set to default value
                        scheme.SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue(), context: context, shouldDirtyScheme: true);
                    }
                    else
                    {
                        // Prompt users that there's an issue, maybe cause a failure in downstream publishing, but allow for editor fixes
                        
                        var fieldData = entryData.Result;
                        var validateData = attribute.IsValidValue(context, fieldData);
                        if (validateData.Failed) // Try to force the manifest to be loaded
                        {
                            //for manifest, handle partial load failures, if a manifest entry refers to a file that doesn't exist
                            Logger.LogDbgWarning($"Entry {entry} failed attribute validate {attribute} in scheme: {scheme}");
                            if (scheme.IsManifest && (attribute.DataType is FilePathDataType || attribute.DataType is FolderDataType))
                            {
                                Logger.LogDbgWarning($"Error validating Manifest data for attribute: {attribute}, {fieldData}, error: {validateData.Message}");
                                continue;
                            }

                            if (attribute.DataType is PluginDataType)
                            {
                                Logger.LogDbgWarning($"Attempting to load a scheme with unknown type: {attribute.DataType.TypeName}");
                                continue;
                            }
                            
                            var conversion = attribute.ConvertValue(context, fieldData);
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
                                    return Fail(context, errorMessage: $"{scheme}.{attribute}: {conversion.Message}");
                                // }
                            }

                            var updateData = scheme.SetDataOnEntry(entry, attribute.AttributeName, conversion.Result, allowIdentifierUpdate: true, context: context, shouldDirtyScheme: false);
                            if (updateData.Failed)
                            {
                                return Fail(context, updateData.Message);
                            }
                        }
                    }
                }
            }
        
            Logger.LogDbgVerbose($"Loading scheme {scheme}");
            loadedSchemes[schemeName] = scheme;
            if (scheme.IsManifest)
            {
                IsTemplateManifestLoaded = false;
            }

            if (registerManifestEntry)
            {
                // add new manifest entry if not existing. Only do this for non-manifest schemes, else this could end up in an stack overflow
                if (scheme.IsManifest || GetManifestEntryForScheme(schemeName, context).Try(out _)) 
                    return Pass("Schema added", context);
                
                // Add a new entry to the manifest for this loaded scheme
                lock (manifestOperationLock)
                {
                    if (!GetManifestEntryForScheme(schemeName, context).Try(out _))
                    {
                        // add manifest record for new scheme
                    
                        Logger.LogDbgVerbose($"Adding manifest entry for {schemeName} with import path: {importFilePath}...", context);
                        var manifest = GetManifestScheme();
                        if (!manifest.Try(out var manifestScheme))
                        {
                            return Fail(context, manifest.Message);
                        }

                        return manifestScheme.AddManifestEntry(context, schemeName, ManifestScheme.PublishTarget.DEFAULT,
                            importFilePath).Cast();
                    }
                }
            }

            return Pass("Schema added", context);
        }

        public static SchemaResult UnloadScheme(SchemaContext context, string schemeName)
        {
            Logger.LogDbgWarning($"Unloading Scheme: {schemeName}");
            bool wasRemoved = loadedSchemes.Remove(schemeName);

            return CheckIf(context: context, conditional: wasRemoved, errorMessage: "Scheme was not unloaded", successMessage: "Scheme was unloaded.");
        }
        #endregion

        #region Save Operations
        public static SchemaResult SaveManifest(SchemaContext context, IProgress<float> progress = null)
        {
            Logger.LogDbgVerbose($"Saving manifest to file: {ManifestImportPath}...", "Manifest");
            if (string.IsNullOrWhiteSpace(ManifestImportPath))
            {
                return Fail(context, "Manifest path is invalid: " + ManifestImportPath);
            }
            // Align behavior with tests: when manifest is not initialized, treat path as invalid rather than initialization error
            if (IsInitialized(context).Failed)
            {
                return Fail(context, "Manifest path is invalid: " + ManifestImportPath);
            }
            
            progress?.Report(0f);
            lock (manifestOperationLock)
            {
                if (!GetManifestSelfEntry(context).Try(out var manifestSelfEntry, out var manifestError))
                {
                    return manifestError.Cast();
                }
                
                var previousManifestPath = manifestSelfEntry.FilePath;
                try
                {
                    // TODO: Unify with FS Data Type
                    var manifestRelativeLoadPath = PathUtility.MakeRelativePath(ManifestImportPath, ProjectPath);
                    manifestSelfEntry.FilePath = manifestRelativeLoadPath;

                    if (_storage.DefaultManifestStorageFormat.SerializeToFile(context, ManifestImportPath, GetManifestScheme().Result._).Passed)
                    {
                        GetManifestScheme().Result.SetDirty(context, false);
                    }
                }
                catch (Exception ex)
                {
                    // rollback bad path
                    manifestSelfEntry.FilePath = previousManifestPath;
                    Logger.LogError(ex.ToString());
                    return Fail(context,$"Failed to save manifest to {ManifestImportPath}\n{ex.Message}");
                }
            }
            
            return Pass($"Saved manifest to path: {ManifestImportPath}");
        }

        public static SchemaResult Save(SchemaContext context, bool saveManifest = false)
        {
            // When explicitly saving the manifest, validate path first to match expected behavior in tests
            if (saveManifest && string.IsNullOrWhiteSpace(ManifestImportPath))
            {
                return Fail(context, "Manifest path is invalid: " + ManifestImportPath);
            }

            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes;
            }
            
            foreach (var scheme in GetSchemes(context))
            {
                if (scheme.IsDirty)
                {
                    if (scheme.IsManifest && !saveManifest)
                    {
                        continue;
                    }
                    
                    var result = SaveDataScheme(context, scheme, saveManifest);
                    if (result.Failed)
                    {
                        return result;
                    }
                }
            }
            
            return Pass("Saved all dirty schemes");
        }

        public static SchemaResult SaveDataScheme(SchemaContext context, DataScheme scheme, bool alsoSaveManifest)
        {
            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes;
            }
            
            Logger.LogDbgVerbose($"Saving {scheme} to file...", "Storage");
            if (scheme == null)
            {
                return Fail(context, "Attempted to save an invalid Data scheme");
            }
            
            var saveStopwatch = Stopwatch.StartNew();
            if (!GetManifestEntryForScheme(scheme.SchemeName, context).Try(out var schemeManifestEntry))
            {
                return Fail(context, "Could not find manifest entry for scheme name: " + scheme.SchemeName);
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
                return Fail(context, sb.ToString());
            }

            // Only serialize the manifest to disk once
            string savePath = ""; 
            SchemaResult result = NoOp;
            if (!scheme.IsManifest)
            {
                // Get the file path from the manifest entry
                savePath = schemeManifestEntry.FilePath;
                
                // TODO: Unify with FS Data Type
                // Resolve relative path if needed
                string resolvedPath = savePath;
                if (!PathUtility.IsAbsolutePath(savePath) && !string.IsNullOrEmpty(ProjectPath))
                {
                    resolvedPath = PathUtility.MakeAbsolutePath(savePath, ProjectPath);
                    
                    // Ensure the directory exists
                    string directory = Path.GetDirectoryName(resolvedPath);
                    if (!_storage.FileSystem.DirectoryExists(context, directory))
                    {
                        _storage.FileSystem.CreateDirectory(context, directory);
                    }
                }
                
                Logger.LogDbgVerbose($"Saving {scheme} to file {resolvedPath} (from path {savePath})", "Storage");
                result = _storage.DefaultSchemeStorageFormat.SerializeToFile(context, resolvedPath, scheme);
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

                if (!manifestScheme.GetSelfEntry(context).Try(out var manifestSelfEntry, out var manifestError))
                {
                    return manifestError.Cast();
                }
                
                if (string.IsNullOrWhiteSpace(manifestSelfEntry.FilePath))
                {
                    manifestSelfEntry.FilePath = Path.GetFileName(ManifestImportPath);
                }
                
                savePath = ManifestImportPath;
                Logger.LogDbgVerbose($"Saving manifest {scheme} to file {ManifestImportPath}", "Storage");
                result = SaveManifest(context);
            }
            saveStopwatch.Stop();

            if (result.Passed)
            {
                Logger.LogDbgVerbose($"Saved {scheme} to file {savePath} in {saveStopwatch.ElapsedMilliseconds:N0} ms", context: "Storage");
                scheme.SetDirty(context, false);
            }
            return result;
        }
        
        #endregion
        
        #endregion
    }
}