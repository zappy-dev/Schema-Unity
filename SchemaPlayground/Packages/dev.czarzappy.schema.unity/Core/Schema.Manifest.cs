using System;
using System.IO;
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
        public static event Action ManifestUpdated;
        private static ManifestScheme _loadedManifestScheme;
        private static ManifestScheme nextManifestScheme;

        public static ManifestScheme LoadedManifestScheme
        {
            get => _loadedManifestScheme;
            private set
            {
                if (_loadedManifestScheme != null && _loadedManifestScheme == value)
                {
                    return;
                }
                _loadedManifestScheme = value;

                ManifestUpdated?.Invoke();
            }
        }
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

                // No-op
                if (manifestImportPath == value)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(manifestImportPath))
                {
                    throw new InvalidOperationException($"Attempt to set Manifest import path is already set to {manifestImportPath}");
                }
                
                manifestImportPath = value;
            }
        }

        public static string ProjectPath { get; set; }
        
        private static string DefaultContentPath => Path.Combine(ProjectPath, DefaultContentDirectory);
        public static string DefaultContentDirectory = "Content";

        public static string GetContentPath(string schemeFileName)
        {
            return Path.Combine(DefaultContentPath, schemeFileName);
        }
        
        public static string ContentLoadPath => DefaultContentPath;
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
        public static SchemaResult<ManifestEntry> GetManifestSelfEntry(SchemaContext context)
        {
            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<ManifestEntry>();
            }

            if (!GetManifestScheme(context).Try(out var manifestScheme, out var manifestError))
            {
                return manifestError.CastError<ManifestEntry>();
            }

            return manifestScheme.GetSelfEntry(context);
        }
        
        #endregion

        #region Template Manifest Operations

        private static bool isTemplateManifestLoaded = false;
        /// <summary>
        /// Flag for figuring out whether we're using the template default manifest or a loaded manifest
        /// </summary>
        private static bool IsTemplateManifestLoaded
        {
            get => isTemplateManifestLoaded;
            set
            {
                Logger.LogDbgVerbose($"IsTemplateManifestLoaded: {value}");
                isTemplateManifestLoaded = value;
            }
        }
        
        /// <summary>
        /// Initializes the template manifest scheme and loads it into the schema system.
        /// </summary>
        /// <returns>A <see cref="SchemaResult"/> indicating success or failure.</returns>
        public static SchemaResult InitializeTemplateManifestScheme(SchemaContext context, string defaultScriptExportPath = "")
        {
            if (!GetStorage(context).Try(out var storage, out var storageErr))
            {
                return storageErr.Cast();
            }
            
            lock (manifestOperationLock)
            {
                // build template
                var templateManifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(context, 
                    defaultScriptExportPath,
                    Path.Combine(DefaultContentDirectory, "Manifest.json"));
                
                // Only attempt to create the directory if a non-empty path is provided
                if (!string.IsNullOrWhiteSpace(defaultScriptExportPath))
                {
                    var createRes = storage.FileSystem.CreateDirectory(context, defaultScriptExportPath);
                    if (createRes.Failed)
                    {
                        return createRes;
                    }
                }
                
                // Prime the manifest as loaded before validation to avoid FS path checks requiring initialization
                LoadedManifestScheme = new ManifestScheme(templateManifestScheme._);
                var result = LoadDataScheme(context, templateManifestScheme._, overwriteExisting: true);
                Logger.LogDbgVerbose(result.Message, result.Context);
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
        public static SchemaResult<ManifestScheme> GetManifestScheme(SchemaContext context = default)
        {
            var res = SchemaResult<ManifestScheme>.New(context);
            
            
            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<ManifestScheme>();
            }

            // if we are currently in the process of loading a new manifest, use that manifest instead
            if (IsManifestLoadInProgress)
            {
                return res.Pass(nextManifestScheme, "Using next manifest in load");
            }

            if (_loadedManifestScheme != null)
            {
                return res.Pass(_loadedManifestScheme, "Manifest scheme is already loaded");
            }

            if (!GetScheme(Manifest.MANIFEST_SCHEME_NAME, context).Try(out var manifest, 
                    out var manifestError))
            {
                return manifestError.CastError<ManifestScheme>();
            }

            var manifestScheme = new ManifestScheme(manifest);
            LoadedManifestScheme = manifestScheme;
            
            return res.Pass(manifestScheme, "Manifest scheme is loaded");
        }

        /// <summary>
        /// Gets the manifest entry for a given data scheme.
        /// </summary>
        /// <param name="scheme">The data scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        public static SchemaResult<ManifestEntry> GetManifestEntryForScheme(DataScheme scheme, SchemaContext context = default)
        {
            var res = SchemaResult<ManifestEntry>.New(context);
            
            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<ManifestEntry>();
            }
            
            return GetManifestEntryForScheme(scheme.SchemeName, context);
        }
        
        /// <summary>
        /// Gets the manifest entry for a given scheme name.
        /// </summary>
        /// <param name="schemeName">The name of the scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        public static SchemaResult<ManifestEntry> GetManifestEntryForScheme(string schemeName, SchemaContext context = default)
        {
            var res = SchemaResult<ManifestEntry>.New(schemeName);

            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<ManifestEntry>();
            }

            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return res.Fail("Invalid scheme name");
            }

            lock (manifestOperationLock)
            {
                if (!GetManifestScheme(context).Try(out var manifestScheme, out var manifestError))
                    return manifestError.CastError<ManifestEntry>();
                
                return manifestScheme.GetEntryForSchemeName(context, schemeName);

            }
        }
    }
}