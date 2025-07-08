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
    public static class Schema
    {
        #region Static Fields and Constants

        public static string ManifestLoadPath
        {
            get
            {
                if (!IsInitialized)
                {
                    return null;
                }

                return ManifestSelfEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
            }
        }

        private static readonly Dictionary<string, DataScheme> dataSchemes = new Dictionary<string, DataScheme>();
        
        /// <summary>
        /// Returns all the available valid scheme names.
        /// </summary>
        public static IEnumerable<string> AllSchemes
        {
            get
            {
                if (!IsInitialized)
                {
                    return Enumerable.Empty<string>();
                }

                // return dataSchemes.Keys;

                lock (manifestOperationLock)
                {
                    if (!GetManifestScheme().Try(out var manifestScheme))
                    {
                        return Enumerable.Empty<string>();
                    }
                    
                    return manifestScheme.GetValuesForAttribute(MANIFEST_ATTRIBUTE_SCHEME_NAME)
                        .Select(a => a?.ToString())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        ;
                }
            }
        }

        public static int NumAvailableSchemes => AllSchemes.Count();
        public static IEnumerable<DataScheme> GetSchemes()
        {
            foreach (var schemeName in AllSchemes)
            {
                if (GetScheme(schemeName).Try(out var scheme))
                {
                    yield return scheme;
                }
            }
        }
        
        #region Manifest Schema Definition
        
        public const string MANIFEST_SCHEME_NAME = "Manifest";
        public const string MANIFEST_ATTRIBUTE_FILEPATH = "FilePath";
        public const string MANIFEST_ATTRIBUTE_SCHEME_NAME = "SchemeName";

        // private static DataScheme ManifestScheme
        // {
        //     get
        //     {
        //         if (!IsInitialized)
        //         {
        //             return null;
        //         }
        //
        //         bool isLoaded = GetScheme(MANIFEST_SCHEME_NAME).Try(out var scheme);
        //         return isLoaded ? scheme : throw new InvalidOperationException("No manifest scheme found!");
        //     }
        // }

        private static SchemaResult<DataScheme> GetManifestScheme()
        {
            if (!IsInitialized)
            {
                return SchemaResult<DataScheme>.Fail("Attempting to access Manifest before initialization.", Context.Manifest);
            }

            bool isLoaded = GetScheme(MANIFEST_SCHEME_NAME).Try(out var scheme);
            return SchemaResult<DataScheme>.CheckIf(isLoaded, scheme, "No manifest scheme found!", "Manifest scheme is loaded", Context.Manifest);
            // return isLoaded ? scheme : throw new InvalidOperationException("No manifest scheme found!");
        }

        private static DataEntry ManifestSelfEntry
        {
            get
            {
                if (!IsInitialized)
                {
                    return null;
                }

                if (!GetManifestScheme().Try(out var manifestScheme))
                {
                    return null;
                }
                
                if (!manifestScheme.TryGetEntry(e =>
                            MANIFEST_SCHEME_NAME.Equals(e.GetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME)),
                        out var manifestSelfEntry))
                {
                    return null;
                }
                
                return manifestSelfEntry;

            }
        }
        
        #endregion
        
        public static bool IsInitialized { get; private set; }
        private static SchemaResult InitResult;

        #endregion
        
        static Schema()
        {
            // initialize template schemes of data
            Reset();
        }

        public static void Reset()
        {
            IsInitialized = false;
            dataSchemes.Clear();

            var initResult = InitializeTemplateManifestScheme();
            IsInitialized = initResult.Passed;
            InitResult = initResult;
        }

        private static SchemaResult InitializeTemplateManifestScheme()
        {
            lock (manifestOperationLock)
            {
                var templateManifestScheme = BuildTemplateManifestSchema();
                return LoadDataScheme(templateManifestScheme, true);
            }
        }

        public static DataScheme BuildTemplateManifestSchema()
        {
            var templateManifestScheme = new DataScheme(MANIFEST_SCHEME_NAME);
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = MANIFEST_ATTRIBUTE_SCHEME_NAME,
                DataType = DataType.Text,
                DefaultValue = DataType.Text.DefaultValue,
                IsIdentifier = true,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth
            });
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = MANIFEST_ATTRIBUTE_FILEPATH,
                DataType = new FilePathDataType(allowEmptyPath:true),
                DefaultValue = DataType.Text.DefaultValue,
                IsIdentifier = false,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
            });
            
            var manifestSelfEntry = new DataEntry(new Dictionary<string, object>
            {
                { MANIFEST_ATTRIBUTE_SCHEME_NAME, MANIFEST_SCHEME_NAME },
                { MANIFEST_ATTRIBUTE_FILEPATH, "" },
            });
            templateManifestScheme.AddEntry(manifestSelfEntry);
            return templateManifestScheme;
        }

        #region Interface Commands

        public static bool DoesSchemeExist(string schemeName)
        {
            return dataSchemes.ContainsKey(schemeName);
        }

        // TODO support async
        public static SchemaResult<DataScheme> GetScheme(string schemeName)
        {
            if (!IsInitialized)
            {
                return SchemaResult<DataScheme>.Fail("Scheme not initialized!", Context.Schema);
            }
            
            var success = dataSchemes.TryGetValue(schemeName, out var scheme);
            return SchemaResult<DataScheme>.CheckIf(success, scheme, 
                errorMessage: $"Scheme '{schemeName}' is not loaded.",
                successMessage: $"Scheme '{schemeName}' is loaded.");
        }
        
        /// <summary>
        /// Load a new scheme into memory.
        /// Note: This does not persist the new data scheme to disk
        /// </summary>
        /// <param name="scheme">New scheme to load</param>
        /// <param name="overwriteExisting">If true, overwrites an existing scheme. If false, fails to overwrite an existing scheme if it exists</param>
        /// <param name="importFilePath">File path from where this scheme was imported, if imported</param>
        /// <returns></returns>
        public static SchemaResult LoadDataScheme(DataScheme scheme, bool overwriteExisting, string importFilePath = null)
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

            // process all incoming entry data and make sure it is in validate formats 
            foreach (var entry in scheme.AllEntries)
            {
                foreach (var attribute in scheme.GetAttributes())
                {
                    var entryData = entry.GetData(attribute);
                    if (entryData.Failed)
                    {
                        scheme.SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue());
                    }
                    else
                    {
                        var fieldData = entryData.Result;
                        var validateData = attribute.DataType.CheckIfValidData(fieldData);
                        if (validateData.Failed)
                        {
                            
                            var conversion = attribute.DataType.ConvertData(fieldData);
                            if (conversion.Failed)
                            {
                                return Fail(conversion.Message, scheme);
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
            // add new manifest entry if not existing. Only do this for non-manifest schemes, else this could end up in an stack overflow
            if (scheme.IsManifest || GetManifestEntryForScheme(schemeName).Try(out _)) 
                return Pass("Schema added", scheme);
            
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
                    
                    Logger.Log($"Adding manifest entry for {schemeName} with import path: {importFilePath}...", "Manifest");
                    if (!GetManifestScheme().Try(out var manifestScheme))
                    {
                        return Fail("Failed to get manifest scheme");
                    }
                        
                    return manifestScheme.AddEntry(newSchemeManifestEntry, runDataValidation: false);
                }
            }

            return Pass("Schema added", scheme);
        }

        private static readonly object manifestOperationLock = new object();
        public static SchemaResult LoadFromManifest(string manifestPath, IProgress<(float, string)> progress = null)
        {
            Logger.Log($"Loading manifest from file: {manifestPath}...", "Manifest");
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return Fail("Manifest path is invalid: " + manifestPath, context: "Manifest");
            }

            if (!Storage.FileSystem.FileExists(manifestPath))
            {
                return Fail($"No Manifest scheme found.\nSearched the following path: {manifestPath}\nLoad an existing manifest scheme or save the empty template.", context: "Manifest");
            }
            
            lock (manifestOperationLock)
            {
                // clear out previous data in case it is stagnant
                var prevDataSchemes = dataSchemes;
                dataSchemes.Clear();
            
                progress?.Report((0f, $"Loading: {manifestPath}..."));
                Logger.Log($"Loading manifest from file: {manifestPath}...", "Manifest");
                if (!Storage.DefaultManifestStorageFormat.DeserializeFromFile(manifestPath)
                        .Try( out var loadedManifestSchema))
                {
                    return Fail("Failed to load manifest schema.", context: "Manifest");
                }
                
                var loadStopwatch = Stopwatch.StartNew();
                int currentSchema = 0;
                int schemeCount = loadedManifestSchema.EntryCount;
                
                var saveManifestResponse = LoadDataScheme(loadedManifestSchema, true);
                if (!saveManifestResponse.Passed)
                {
                    return saveManifestResponse;
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
                        continue;
                    }
                    
                    loadedSchemes.Add(loadedScheme);
                }
                
                // TODO: topological sort so schemes are loaded in reference order
                var schemeLoadOrder = loadedSchemes.OrderBy(scheme => scheme.GetReferenceAttributes().Count() );

                progress?.Report((0.1f, "Loading..."));
                foreach (var scheme in schemeLoadOrder)
                {
                    var loadScheme = LoadDataScheme(scheme,
                        overwriteExisting: false);
                    if (loadScheme.Failed)
                    {
                        success = false;
                        sb.AppendLine(loadScheme.Message);
                    }
                }

                if (!success)
                {
                    return Fail($"Failed to load manifest from {manifestPath}, errors: {sb}", context: "Manifest");
                }
                
                // overwrite existing manifest
                loadStopwatch.Stop();
            
                return Pass($"Loaded {schemeCount} schemes from manifest in {loadStopwatch.ElapsedMilliseconds:N0} ms", context: "Manifest");
            }
        }

        internal static class Context
        {
            public const string DataConversion = "Conversion";
            public const string Manifest = "Manifest";
            public const string Schema = "Schema";
            
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

            if (string.IsNullOrWhiteSpace(schemeFilePath) || !Storage.FileSystem.FileExists(schemeFilePath))
            {
                return SchemaResult<DataScheme>.Fail($"{manifestEntry} Invalid scheme file path: {schemeFilePath}",
                    Context.Manifest);
            }
                    
            progress?.Report(schemeFilePath);
                
            // TODO support async loading
            if (!Storage.DefaultSchemaStorageFormat.DeserializeFromFile(schemeFilePath)
                    .Try(out var loadedSchema))
            {
                return SchemaResult<DataScheme>.Fail("Failed to load scheme from file.", context: Context.Manifest);
            }
            
            return SchemaResult<DataScheme>.Pass(loadedSchema, $"Loaded scheme data from file", Context.Manifest);
        }

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
                    
                    Storage.DefaultManifestStorageFormat.SerializeToFile(manifestPath, GetManifestScheme().Result);
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
                    var result = SaveDataScheme(scheme, saveManifest);
                    if (result.Failed)
                    {
                        return result;
                    }
                }
            }
            
            return Pass("Saved all dirty schemes");
        }

        public static SchemaResult SaveDataScheme(DataScheme scheme, bool saveManifest)
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
            SchemaResult result = SchemaResult.NoOp;
            if (!isManifestScheme)
            {
                // TODO: Handle if the data doesn't yet have a save path from the manifest
                savePath = schemeManifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
                Logger.Log($"Saving {scheme} to file {savePath}", "Storage");
                result = Storage.DefaultSchemaStorageFormat.SerializeToFile(savePath, scheme);
            }

            if (isManifestScheme && saveManifest)
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

        public static SchemaResult<DataEntry> GetManifestEntryForScheme(DataScheme scheme)
        {
            if (!IsInitialized || scheme is null)
            {
                return SchemaResult<DataEntry>.Fail(errorMessage: "Manifest scheme is not initialized", Context.Manifest);
            }
            
            return GetManifestEntryForScheme(scheme.SchemeName);
        }
        
        internal static SchemaResult<DataEntry> GetManifestEntryForScheme(string schemeName)
        {
            if (!IsInitialized)
            {
                return SchemaResult<DataEntry>.Fail("Manifest scheme is not initialized", Context.Manifest);
            }

            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return SchemaResult<DataEntry>.Fail("Invalid scheme name", Context.Manifest);
            }

            lock (manifestOperationLock)
            {
                bool success = GetManifestScheme().Result.TryGetEntry(e => string.Equals(schemeName, e.GetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME)),
                    out var schemeManifestEntry);
                
                return SchemaResult<DataEntry>.CheckIf(success, schemeManifestEntry, 
                    errorMessage: $"Failed to get manifest entry for scheme '{schemeName}'", 
                    successMessage: $"Found manifest entry for scheme '{schemeName}'", context: Context.Manifest);
            }
        }
        
        #endregion

        public static bool TryGetSchemeForAttribute(AttributeDefinition searchAttr, out DataScheme ownerScheme)
        {
            if (!IsInitialized)
            {
                ownerScheme = null;
                return false;
            }
            
            ownerScheme = dataSchemes.Values.FirstOrDefault(scheme =>
            {
                return scheme.GetAttribute(attr => attr.Equals(searchAttr)).Try(out _);
            });
            
            return ownerScheme != null;
        }

        /// <summary>
        /// Updates an identifier value in the specified scheme and propagates the change to all referencing entries in all loaded schemes.
        /// </summary>
        /// <param name="schemeName">The name of the scheme containing the identifier to update.</param>
        /// <param name="identifierAttribute">The name of the identifier attribute to update.</param>
        /// <param name="oldValue">The old identifier value to be replaced.</param>
        /// <param name="newValue">The new identifier value to set.</param>
        /// <returns>A SchemaResult indicating success or failure, and the number of references updated.</returns>
        public static SchemaResult UpdateIdentifierValue(string schemeName, string identifierAttribute, object oldValue, object newValue)
        {
            // 1. Update the identifier value in the specified scheme
            if (!GetScheme(schemeName).Try(out var targetScheme))
                return Fail($"Scheme '{schemeName}' not found.");
            
            var entry = targetScheme.AllEntries.FirstOrDefault(e => Equals(e.GetDataAsString(identifierAttribute), oldValue?.ToString()));
            if (entry == null)
                return Fail($"Entry with {identifierAttribute} == '{oldValue}' not found in scheme '{schemeName}'.");
            
            var idUpdateResult = targetScheme.SetDataOnEntry(entry, identifierAttribute, newValue, allowIdentifierUpdate: true);
            if (idUpdateResult.Failed)
                return idUpdateResult;
            int totalUpdated = 0;
            // 2. Propagate to all referencing entries in all loaded schemes
            foreach (var scheme in GetSchemes())
            {
                if (scheme.SchemeName == schemeName)
                    continue;
                totalUpdated += scheme.UpdateReferencesToIdentifier(schemeName, identifierAttribute, oldValue, newValue);
            }
            return Pass($"Updated identifier value from '{oldValue}' to '{newValue}' in '{schemeName}'. Updated {totalUpdated} references.");
        }
    }
}