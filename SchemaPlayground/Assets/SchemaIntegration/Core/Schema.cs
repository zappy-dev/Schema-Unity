using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Schema.Core.Serialization;

namespace Schema.Core
{
    public static class Schema
    {
        #region Static Fields and Constants
        
        public static string ManifestPath { get; private set; } 
        private static readonly Dictionary<string, DataScheme> dataSchemes = new Dictionary<string, DataScheme>();
        
        public static IEnumerable<string> AllSchemes => Manifest.Entries.Select(e => e[MANIFEST_ATTRIBUTE_SCHEMANAME].ToString());

        public static DataEntry GetSchemaManifest(string schemaName)
        {
            return Manifest.Entries.FirstOrDefault(e => e[MANIFEST_ATTRIBUTE_SCHEMANAME].ToString() == schemaName);
        }
        
        public const string MANIFEST_SCHEMA_NAME = "Manifest";
        public const string MANIFEST_ATTRIBUTE_FILEPATH = "FilePath";
        public const string MANIFEST_ATTRIBUTE_SCHEMANAME = "SchemaName";

        private static DataScheme Manifest
        {
            get => dataSchemes[MANIFEST_SCHEMA_NAME];
            set
            {
                dataSchemes[MANIFEST_SCHEMA_NAME] = value;
            }
        }

        #endregion
        
        static Schema()
        {
            Manifest = new DataScheme(MANIFEST_SCHEMA_NAME)
            {
                Attributes = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = MANIFEST_ATTRIBUTE_SCHEMANAME,
                        DataType = DataType.String,
                        DefaultValue = DataType.String.DefaultValue,
                        IsIdentifier = true,
                        ColumnWidth = AttributeDefinition.DefaultColumnWidth
                    },
                    new AttributeDefinition
                    {
                        
                        AttributeName = MANIFEST_ATTRIBUTE_FILEPATH,
                        DataType = DataType.String,
                        DefaultValue = DataType.String.DefaultValue,
                        IsIdentifier = false,
                        ColumnWidth = AttributeDefinition.DefaultColumnWidth,
                    },
                },
            };
        }

        #region Interface Commands

        public static bool DoesSchemaExist(string name)
        {
            return dataSchemes.ContainsKey(name);
        }

        // TODO support async
        public static bool TryGetSchema(string schemaName, out DataScheme scheme)
        {
            return dataSchemes.TryGetValue(schemaName, out scheme);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scheme">New schema to load</param>
        /// <param name="overwriteExisting">If true, overwrites an existing schema. If false, fails to overwrite an existing scheme if it exists</param>
        /// <param name="importFilePath">File path from where this schema was imported, if imported</param>
        /// <returns></returns>
        public static SchemaResponse AddSchema(DataScheme scheme, bool overwriteExisting, string importFilePath = null)
        {
            string schemaName = scheme.SchemaName;
            
            // input validation
            if (string.IsNullOrEmpty(schemaName))
            {
                return SchemaResponse.Error("Schema name is invalid: " + schemaName);
            }
            
            if (dataSchemes.ContainsKey(schemaName) && !overwriteExisting)
            {
                return SchemaResponse.Error("Schema already exists: " + schemaName);
            }
        
            dataSchemes[schemaName] = scheme;
            if (GetSchemaManifest(schemaName) == null)
            {
                // add manifest record for new schema
                var manifestRecord = new DataEntry(
                    new Dictionary<string, object>
                    {
                        { MANIFEST_ATTRIBUTE_SCHEMANAME, schemaName },
                    }
                );
            
                if (!string.IsNullOrEmpty(importFilePath))
                {
                    // TODO: Record import path or give option to clone / copy file to new content path?
                    manifestRecord.SetData(MANIFEST_ATTRIBUTE_FILEPATH, importFilePath);
                }
                
                Manifest.Entries.Add(manifestRecord);
            }
            
            var saveResponse = SaveSchema(scheme, saveManifest: true);

            if (!saveResponse.IsSuccess)
            {
                return saveResponse;
            }
            
            return SchemaResponse.Success($"Schema added: {schemaName}");
        }

        private static object opLoc = new object();
        public static SchemaResponse LoadFromManifest(string manifestPath, IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(manifestPath))
            {
                return SchemaResponse.Error("Manifest path is invalid: " + manifestPath);
            }

            if (!File.Exists(manifestPath))
            {
                return SchemaResponse.Error("Manifest file not found: " + manifestPath);
            }

            ManifestPath = manifestPath;
            
            lock (opLoc)
            {
                // clear out previous data in case it is stagnant
                dataSchemes.Clear();
            
                progress?.Report(0f);
                Manifest = Storage.DefaultManifestStorageFormat.Load(manifestPath);
                var loadStopwatch = Stopwatch.StartNew();

                int currentSchema = 0;
                int schemaCount = Manifest.Entries.Count;
                foreach (var manifestEntry in Manifest.Entries)
                {
                    currentSchema++;
                    progress?.Report(currentSchema * 1.0f / schemaCount);
                    if (!manifestEntry.TryGetDataAsString(MANIFEST_ATTRIBUTE_FILEPATH, out var schemaFilePath))
                    {
                        // TODO: Report partial load failure
                        
                        continue;
                    }
                
                    // TODO support async loading
                    var loadedSchema = Storage.DefaultSchemaStorageFormat.Load(schemaFilePath);
                    AddSchema(loadedSchema, true);
                }
                loadStopwatch.Stop();
            
                return SchemaResponse.Success($"Loaded {schemaCount} schemas from manifest in {loadStopwatch.ElapsedMilliseconds:N0} ms");
            }
        }

        public static SchemaResponse SaveManifest(string manifestPath, IProgress<float> progress = null)
        {
            if (string.IsNullOrEmpty(manifestPath))
            {
                return SchemaResponse.Error("Manifest path is invalid: " + manifestPath);
            }
            
            progress?.Report(0f);
            lock (opLoc)
            {
                Storage.DefaultManifestStorageFormat.Save(manifestPath, Manifest);
            }
            
            return SchemaResponse.Success("Saved manifest to manifest.");
        }

        public static SchemaResponse SaveSchema(DataScheme scheme, bool saveManifest = true)
        {
            var saveStopwatch = Stopwatch.StartNew();
            var schemaManifest = GetSchemaManifest(scheme.SchemaName);
            string filePath = schemaManifest[MANIFEST_ATTRIBUTE_FILEPATH].ToString();
            Storage.DefaultSchemaStorageFormat.Save(filePath, scheme);

            if (saveManifest)
            {
                var saveManifestResponse = SaveManifest(ManifestPath);
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
            
            return SchemaResponse.Success($"Saved {scheme} to file {filePath} in {saveStopwatch.ElapsedMilliseconds:N0} ms");
        }
        
        #endregion
    }

    public enum RequestStatus
    {
        Error,
        Success
    }
}