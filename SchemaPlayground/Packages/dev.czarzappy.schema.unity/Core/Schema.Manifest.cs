using System.Collections.Generic;
using Schema.Core.Data;

namespace Schema.Core
{
    /// <summary>
    /// Provides manifest schema management and operations for the Schema system.
    /// This partial class contains methods and properties for handling the manifest scheme and its entries.
    /// </summary>
    public partial class Schema
    {
        #region Manifest Schema Definition
        /// <summary>
        /// The name of the manifest scheme.
        /// </summary>
        public const string MANIFEST_SCHEME_NAME = "Manifest";
        /// <summary>
        /// The attribute name for the file path in the manifest.
        /// </summary>
        public const string MANIFEST_ATTRIBUTE_FILEPATH = "FilePath";
        /// <summary>
        /// The attribute name for the scheme name in the manifest.
        /// </summary>
        public const string MANIFEST_ATTRIBUTE_SCHEME_NAME = "SchemeName";
        #endregion

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

        #region Properties
        /// <summary>
        /// Gets the manifest's own entry from the manifest scheme, or null if not initialized.
        /// </summary>
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

        #region Template Manifest Operations
        /// <summary>
        /// Initializes the template manifest scheme and loads it into the schema system.
        /// </summary>
        /// <returns>A <see cref="SchemaResult"/> indicating success or failure.</returns>
        private static SchemaResult InitializeTemplateManifestScheme()
        {
            lock (manifestOperationLock)
            {
                var templateManifestScheme = BuildTemplateManifestSchema();
                return LoadDataScheme(templateManifestScheme, overwriteExisting: true);
            }
        }

        /// <summary>
        /// Builds a template manifest schema with default attributes and a self-entry.
        /// </summary>
        /// <returns>A <see cref="DataScheme"/> representing the template manifest schema.</returns>
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
        #endregion

        /// <summary>
        /// Gets the manifest scheme if loaded and initialized.
        /// </summary>
        /// <returns>A <see cref="SchemaResult{DataScheme}"/> indicating success or failure, and the manifest scheme if successful.</returns>
        private static SchemaResult<DataScheme> GetManifestScheme()
        {
            if (!IsInitialized)
            {
                return SchemaResult<DataScheme>.Fail("Attempting to access Manifest before initialization.", Context.Manifest);
            }

            bool isLoaded = GetScheme(MANIFEST_SCHEME_NAME).Try(out var scheme);
            return SchemaResult<DataScheme>.CheckIf(isLoaded, scheme, "No manifest loaded!", "Manifest scheme is loaded", Context.Manifest);
        }

        /// <summary>
        /// Gets the manifest entry for a given data scheme.
        /// </summary>
        /// <param name="scheme">The data scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        public static SchemaResult<DataEntry> GetManifestEntryForScheme(DataScheme scheme)
        {
            if (!IsInitialized || scheme is null)
            {
                return SchemaResult<DataEntry>.Fail(errorMessage: "Manifest scheme is not initialized", Context.Manifest);
            }
            
            return GetManifestEntryForScheme(scheme.SchemeName);
        }
        
        /// <summary>
        /// Gets the manifest entry for a given scheme name.
        /// </summary>
        /// <param name="schemeName">The name of the scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
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
                if (GetManifestScheme().Try(out var manifestScheme))
                {
                    bool success = manifestScheme.TryGetEntry(e => string.Equals(schemeName, e.GetDataAsString(MANIFEST_ATTRIBUTE_SCHEME_NAME)),
                        out var schemeManifestEntry);
                    
                    return SchemaResult<DataEntry>.CheckIf(success, schemeManifestEntry, 
                        errorMessage: $"Failed to get manifest entry for scheme '{schemeName}'", 
                        successMessage: $"Found manifest entry for scheme '{schemeName}'", context: Context.Manifest);
                }
                else
                {
                    return SchemaResult<DataEntry>.Fail(errorMessage: "Manifest Scheme not found", Context.Manifest);
                }
            }
        }
    }
}