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
                        
                        AttributeName = MANIFEST_ATTRIBUTE_FILEPATH,
                        DataType = DataType.String,
                        DefaultValue = DataType.String.DefaultValue,
                        ColumnWidth = AttributeDefinition.DefaultColumnWidth
                    },
                    new AttributeDefinition
                    {
                        AttributeName = MANIFEST_ATTRIBUTE_SCHEMANAME,
                        DataType = DataType.String,
                        DefaultValue = DataType.String.DefaultValue,
                        ColumnWidth = AttributeDefinition.DefaultColumnWidth
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
        public static bool TryGetSchema(string selectedSchemaName, out DataScheme scheme)
        {
            return dataSchemes.TryGetValue(selectedSchemaName, out scheme);
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
                var manifestRecord = new DataEntry
                {
                    EntryData = new Dictionary<string, object>
                    {
                        { MANIFEST_ATTRIBUTE_SCHEMANAME, schemaName },
                    }
                };
            
                if (!string.IsNullOrEmpty(importFilePath))
                {
                    // TODO: Record import path or give option to clone / copy file to new content path?
                    manifestRecord.EntryData.Add(MANIFEST_ATTRIBUTE_FILEPATH, importFilePath);
                }
                
                Manifest.Entries.Add(manifestRecord);
            }
            
            var saveResponse = SaveSchema(scheme);

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
                var stopwatch = Stopwatch.StartNew();

                int currentSchema = 0;
                int schemaCount = Manifest.Entries.Count;
                foreach (var manifestEntry in Manifest.Entries)
                {
                    string schemaFilePath = manifestEntry.EntryData[MANIFEST_ATTRIBUTE_FILEPATH].ToString();

                    currentSchema++;
                    progress?.Report(currentSchema * 1.0f / schemaCount);
                
                    // TODO support async loading
                    var loadedSchema = Storage.DefaultSchemaStorageFormat.Load(schemaFilePath);
                    AddSchema(loadedSchema, true);
                }
                stopwatch.Stop();
            
                return SchemaResponse.Success($"Loaded {schemaCount} schemas from manifest in {stopwatch.ElapsedMilliseconds:N0} ms");
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

        public static SchemaResponse SaveSchema(DataScheme scheme)
        {
            var schemaManifest = GetSchemaManifest(scheme.SchemaName);
            string filePath = schemaManifest[MANIFEST_ATTRIBUTE_FILEPATH].ToString();
            Storage.DefaultSchemaStorageFormat.Save(filePath, scheme);

            var saveManifestResponse = SaveManifest(ManifestPath);
            if (!saveManifestResponse.IsSuccess)
            {
                return saveManifestResponse;
            }
            
            return SchemaResponse.Success($"Successfully saved schema for {scheme.SchemaName} to file {filePath}");
        }
        
        #endregion
    }

    public enum RequestStatus
    {
        Error,
        Success
    }

    public struct SchemaResponse
    {
        private RequestStatus status;
        public RequestStatus Status => status;
        private object payload;
        public object Payload => payload;
        public bool IsSuccess => status == RequestStatus.Success;

        public SchemaResponse(RequestStatus status, object payload)
        {
            this.status = status;
            this.payload = payload;
        }
    
        public static SchemaResponse Error(string errorMessage) => 
            new SchemaResponse(status: RequestStatus.Error, payload: errorMessage);

        public static SchemaResponse Success(string message) =>
            new SchemaResponse(status: RequestStatus.Success, payload: message);

        public override string ToString()
        {
            return $"SchemaResponse[status={status}, payload={payload}]";
        }
    }
}