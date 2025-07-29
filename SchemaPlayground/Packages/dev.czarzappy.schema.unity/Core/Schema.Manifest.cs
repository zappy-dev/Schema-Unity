using System;
using Schema.Core.Data;
using Schema.Core.Logging;
using Schema.Core.Schemes;

namespace Schema.Core
{
    /// <summary>
    /// Provides manifest schema management and operations for the Schema system.
    /// This partial class contains methods and properties for handling the manifest scheme and its entries.
    /// </summary>
    public partial class Schema
    {
        private static ManifestScheme loadedManifestScheme;
        private static ManifestScheme nextManifestScheme;
        public static ManifestScheme LoadedManifestScheme => loadedManifestScheme;
        public static bool IsManifestLoadInProgress => nextManifestScheme != null;

        private static string manifestImportPath;

        /// <summary>
        /// The absolute file path from where the Manifest scheme was loaded from.
        /// This should only exist when a Manifest scheme was loaded into memory.
        /// </summary>
        public static string ManifestImportPath
        {
            get => manifestImportPath;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException("Manifest import path cannot be null or empty.");
                }

                if (!string.IsNullOrWhiteSpace(manifestImportPath))
                {
                    throw new InvalidOperationException($"Attempt to set Manifest import path is already set to {manifestImportPath}");
                }
                
                manifestImportPath = value;
            }
        }

        public static string ContentLoadPath
        {
            get
            {
                string manifestDir = System.IO.Path.GetDirectoryName(ManifestImportPath);
                return manifestDir;
            }
        }
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
        public static ManifestEntry ManifestSelfEntry
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

                return manifestScheme.SelfEntry;
            }
        }
        
        #endregion

        #region Template Manifest Operations

        private static bool isTemplateManifestLoaded = false;
        /// <summary>
        /// Flag for figuring out whether we're using the template default manifest or a loaded manifest
        /// </summary>
        private static bool IsTemplateManifestLoaded
        {
            get
            {
                return isTemplateManifestLoaded;
            }
            set
            {
                Logger.LogVerbose($"IsTemplateManifestLoaded: {value}");
                isTemplateManifestLoaded = value;
            }
        }
        
        /// <summary>
        /// Initializes the template manifest scheme and loads it into the schema system.
        /// </summary>
        /// <returns>A <see cref="SchemaResult"/> indicating success or failure.</returns>
        private static SchemaResult InitializeTemplateManifestScheme()
        {
            lock (manifestOperationLock)
            {
                var templateManifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
                var result =  LoadDataScheme(templateManifestScheme._, overwriteExisting: true);
                if (result.Passed)
                {
                    IsTemplateManifestLoaded = true;
                }

                return result;
            }
        }
        #endregion

        /// <summary>
        /// Gets the manifest scheme if loaded and initialized.
        /// </summary>
        /// <returns>A <see cref="SchemaResult{DataScheme}"/> indicating success or failure, and the manifest scheme if successful.</returns>
        public static SchemaResult<ManifestScheme> GetManifestScheme()
        {
            var res = SchemaResult<ManifestScheme>.New(Context.Manifest);
            
            if (!IsInitialized)
            {
                return res.Fail("Attempting to access Manifest before initialization.");
            }

            // if we are currently in the process of loading a new manifest, use that manifest instead
            if (IsManifestLoadInProgress)
            {
                return res.Pass(nextManifestScheme, "Using next manifest in load");
            }

            if (loadedManifestScheme != null)
            {
                return res.Pass(loadedManifestScheme, "Manifest scheme is already loaded");
            }

            var dataSchemeRes = GetScheme(Manifest.MANIFEST_SCHEME_NAME);

            if (dataSchemeRes.Failed)
            {
                return dataSchemeRes.CastError<ManifestScheme>();
            }

            var manifestScheme = new ManifestScheme(dataSchemeRes.Result);
            loadedManifestScheme = manifestScheme;
            
            return res.Pass(manifestScheme, "Manifest scheme is loaded");
        }

        /// <summary>
        /// Gets the manifest entry for a given data scheme.
        /// </summary>
        /// <param name="scheme">The data scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        public static SchemaResult<ManifestEntry> GetManifestEntryForScheme(DataScheme scheme)
        {
            var res = SchemaResult<ManifestEntry>.New(Context.Manifest);
            
            if (!IsInitialized || scheme is null)
            {
                return res.Fail(errorMessage: "Manifest scheme is not initialized");
            }
            
            return GetManifestEntryForScheme(scheme.SchemeName);
        }
        
        /// <summary>
        /// Gets the manifest entry for a given scheme name.
        /// </summary>
        /// <param name="schemeName">The name of the scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        internal static SchemaResult<ManifestEntry> GetManifestEntryForScheme(string schemeName)
        {
            var res = SchemaResult<ManifestEntry>.New(Context.Manifest);
            
            if (!IsInitialized)
            {
                return res.Fail("Manifest scheme is not initialized");
            }

            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return res.Fail("Invalid scheme name");
            }

            lock (manifestOperationLock)
            {
                if (!GetManifestScheme().Try(out var manifestScheme))
                    return res.Fail(errorMessage: "Manifest Scheme not found");
                
                bool success = manifestScheme.TryGetEntryForSchemeName(schemeName, out var schemeManifestEntry);
                    
                return res.CheckIf(success, schemeManifestEntry, 
                    errorMessage: $"Failed to get manifest entry for scheme '{schemeName}'", 
                    successMessage: $"Found manifest entry for scheme '{schemeName}'");

            }
        }
    }
}