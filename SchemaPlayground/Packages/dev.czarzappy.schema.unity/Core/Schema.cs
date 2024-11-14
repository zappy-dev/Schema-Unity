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
                    if (ManifestScheme is null)
                    {
                        return Enumerable.Empty<string>();
                    }
                    
                    return ManifestScheme.GetValuesForAttribute(MANIFEST_ATTRIBUTE_SCHEME_NAME)
                        .Select(a => a?.ToString())
                        .Where(a => !string.IsNullOrEmpty(a))
                        ;
                }
            }
        }
        
        public const string MANIFEST_SCHEME_NAME = "Manifest";
        public const string MANIFEST_ATTRIBUTE_FILEPATH = "FilePath";
        public const string MANIFEST_ATTRIBUTE_SCHEME_NAME = "SchemeName";

        private static DataScheme ManifestScheme
        {
            get
            {
                if (!IsInitialized)
                {
                    return null;
                }
                
                return TryGetScheme(MANIFEST_SCHEME_NAME, out var scheme) ? scheme : throw new InvalidOperationException("Manifest scheme not found.");
            }
        }

        private static DataEntry ManifestSelfEntry
        {
            get
            {
                if (!IsInitialized)
                {
                    return null;
                }
                
                if (!ManifestScheme.TryGetEntry(e =>
                            MANIFEST_SCHEME_NAME.Equals(e.GetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME)),
                        out var manifestSelfEntry))
                {
                    return null;
                }
                
                return manifestSelfEntry;

            }
        }

        public static bool IsInitialized { get; private set; }

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
            
            InitializeTemplateManifestScheme();
            IsInitialized = true;
        }

        private static void InitializeTemplateManifestScheme()
        {
            lock (manifestOperationLock)
            {
                var templateManifestScheme = BuildTemplateManifestSchema();
                LoadDataScheme(templateManifestScheme, true);
            }
        }

        public static DataScheme BuildTemplateManifestSchema()
        {
            var manifestSelfEntry = new DataEntry(new Dictionary<string, object>
            {
                { MANIFEST_ATTRIBUTE_SCHEME_NAME, MANIFEST_SCHEME_NAME },
                { MANIFEST_ATTRIBUTE_FILEPATH, "" },
            });
            
            var templateManifestScheme = new DataScheme(MANIFEST_SCHEME_NAME);
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = MANIFEST_ATTRIBUTE_SCHEME_NAME,
                DataType = DataType.String,
                DefaultValue = DataType.String.DefaultValue,
                IsIdentifier = true,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth
            });
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = MANIFEST_ATTRIBUTE_FILEPATH,
                DataType = DataType.FilePath,
                DefaultValue = DataType.String.DefaultValue,
                IsIdentifier = false,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
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
        public static bool TryGetScheme(string schemeName, out DataScheme scheme)
        {
            return dataSchemes.TryGetValue(schemeName, out scheme);
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
            if (string.IsNullOrEmpty(schemeName))
            {
                return Fail("Schema name is invalid: " + schemeName, scheme);
            }
            
            if (dataSchemes.ContainsKey(schemeName) && !overwriteExisting)
            {
                return Fail("Schema already exists: " + schemeName);
            }
        
            dataSchemes[schemeName] = scheme;
            // add new manifest entry if not existing. Only do this for non-manifest schemes, else this could end up in an stack overflow
            if (!scheme.IsManifest &&
                !TryGetManifestEntryForScheme(schemeName, out _))
            {
                lock (manifestOperationLock)
                {
                    if (!TryGetManifestEntryForScheme(schemeName, out _))
                    {
                        // add manifest record for new scheme
                        var newSchemeManifestEntry = new DataEntry();
                        newSchemeManifestEntry.SetData(MANIFEST_ATTRIBUTE_SCHEME_NAME, schemeName);
            
                        if (!string.IsNullOrEmpty(importFilePath))
                        {
                            // TODO: Record import path or give option to clone / copy file to new content path?
                            newSchemeManifestEntry.SetData(MANIFEST_ATTRIBUTE_FILEPATH, importFilePath);
                        }
                        
                        ManifestScheme.AddEntry(newSchemeManifestEntry);
                    }
                }
            }
            
            return Success("Schema added", scheme);
        }

        private static readonly object manifestOperationLock = new object();
        public static SchemaResult LoadFromManifest(string manifestPath, IProgress<(float, string)> progress = null)
        {
            Logger.Log($"Loading manifest from file: {manifestPath}...", "Manifest");
            if (string.IsNullOrEmpty(manifestPath))
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
                if (!Storage.DefaultManifestStorageFormat.TryDeserializeFromFile(manifestPath, out var loadedManifestSchema))
                {
                    return Fail("Failed to load manifest schema.", context: "Manifest");
                }
                
                var loadStopwatch = Stopwatch.StartNew();
                int currentSchema = 0;
                int schemeCount = loadedManifestSchema.EntryCount;
                
                var saveManifestResponse = LoadDataScheme(loadedManifestSchema, true);
                if (!saveManifestResponse.IsSuccess)
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
                
                foreach (var manifestEntry in schemaGroupsArray.Select(g => g.First()))
                {
                    currentSchema++;
                    Logger.Log($"Handling entry {manifestEntry}...", "Manifest");

                    var loadResponse = LoadEntryFromManifest(manifestEntry, 
                        progress: new Progress<string>(schemeFilePath =>
                    {
                        progress?.Report((currentSchema * 1.0f / schemeCount, $"Loading ({currentSchema}/{schemeCount}): {schemeFilePath}"));
                    }));

                    if (loadResponse.Failed)
                    {
                        success = false;
                    }
                }

                if (!success)
                {
                    return Fail($"Failed to load manifest from {manifestPath}, errors: {sb}", context: "Manifest");
                }
                
                // overwrite existing manifest
                loadStopwatch.Stop();
            
                return Success($"Loaded {schemeCount} schemes from manifest in {loadStopwatch.ElapsedMilliseconds:N0} ms", context: "Manifest");
            }
        }

        private static class Context
        {
            public const string Manifest = "Manifest";
        }
        
        internal static SchemaResult LoadEntryFromManifest(DataEntry manifestEntry,
            IProgress<string> progress = null)
        {
            if (manifestEntry == null)
            {
                return Fail($"Failed to load manifest from {manifestEntry}", Context.Manifest);
            }
            
            // valid manifest entries
            if (!manifestEntry.TryGetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME, out var entrySchemaName))
            {
                return Fail($"Failed to parse manifest entry '{manifestEntry}'", Context.Manifest);
            }

            if (string.IsNullOrEmpty(entrySchemaName))
            {
                return Fail($"Failed to parse manifest entry '{manifestEntry}'", Context.Manifest);
            }

            if (entrySchemaName.Equals(MANIFEST_SCHEME_NAME))
            {
                return Success($"Skipping importing manifest entry");
            }
                    
            if (!manifestEntry.TryGetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH, out var schemeFilePath))
            {
                // TODO: Report partial load failure
                return Fail($"No attribute data for '{MANIFEST_ATTRIBUTE_SCHEME_NAME}' in {manifestEntry}");
            }

            if (string.IsNullOrEmpty(schemeFilePath) || !Storage.FileSystem.FileExists(schemeFilePath))
            {
                return Fail($"{manifestEntry} Invalid scheme file path: {schemeFilePath}");
            }
                    
            progress?.Report(schemeFilePath);
                
            // TODO support async loading
            if (!Storage.DefaultSchemaStorageFormat.TryDeserializeFromFile(schemeFilePath,
                    out var loadedSchema))
            {
                return Fail("Failed to load schema from file.", context: "Manifest");
            }
                    
            // allow loaded manifest to overwrite in-memory manifest
            bool isSelfEntry = loadedSchema.IsManifest;
            if (isSelfEntry)
            {
                // TODO: How best to handle loading the manifest schema while already loading the manifest schema?
                // Add validation to make sure it is the same file path?
                return Success($"Skipping importing manifest entry {manifestEntry}");
            }
            
            return LoadDataScheme(loadedSchema,
                overwriteExisting: false);
        }

        public static SchemaResult SaveManifest(string manifestPath, IProgress<float> progress = null)
        {
            Logger.Log($"Saving manifest to file: {manifestPath}...", "Manifest");
            if (string.IsNullOrEmpty(manifestPath))
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
                    Storage.DefaultManifestStorageFormat.SerializeToFile(manifestPath, ManifestScheme);
                }
                catch (Exception ex)
                {
                    ManifestSelfEntry.SetData(MANIFEST_ATTRIBUTE_FILEPATH, previousManifestPath);
                    Logger.LogError(ex.ToString());
                    return Fail($"Failed to save manifest to {manifestPath}\n{ex.Message}");
                }
            }
            
            return Success($"Saved manifest to path: {manifestPath}");
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
            if (!TryGetManifestEntryForScheme(scheme.SchemeName, out var schemeManifestEntry))
            {
                return Fail("Could not find manifest entry for scheme name: " + scheme.SchemeName);
            }

            // Only serialize the manifest to disk once
            string savePath = ""; 
            if (!isManifestScheme)
            {
                // TODO: Handle if the data doesn't yet have a save path from the manifest
                savePath = schemeManifestEntry.GetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH);
                Storage.DefaultSchemaStorageFormat.SerializeToFile(savePath, scheme);
            }

            if (saveManifest || isManifestScheme)
            {
                savePath = ManifestLoadPath;
                var saveManifestResponse = SaveManifest(ManifestLoadPath);
                saveStopwatch.Stop();
                if (!saveManifestResponse.IsSuccess)
                {
                    return saveManifestResponse;
                }
            }
            else
            {
                saveStopwatch.Stop();
            }
            
            return Success($"Saved to file {savePath} in {saveStopwatch.ElapsedMilliseconds:N0} ms", scheme);
        }

        public static bool TryGetManifestEntryForScheme(DataScheme scheme, out DataEntry schemeManifestEntry)
        {
            if (!IsInitialized)
            {
                schemeManifestEntry = null;
                return false;
            }

            if (scheme is null)
            {
                schemeManifestEntry = null;
                return false;
            }
            
            return TryGetManifestEntryForScheme(scheme.SchemeName, out schemeManifestEntry);
        }
        
        internal static bool TryGetManifestEntryForScheme(string schemeName, out DataEntry schemeManifestEntry)
        {
            if (!IsInitialized)
            {
                schemeManifestEntry = null;
                return false;
            }

            if (string.IsNullOrEmpty(schemeName))
            {
                schemeManifestEntry = null;
                return false;
            }

            lock (manifestOperationLock)
            {
                return ManifestScheme.TryGetEntry(e => string.Equals(schemeName, e.GetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME)),
                    out schemeManifestEntry);
            }
        }
        
        #endregion
    }
}