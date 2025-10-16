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
        [Obsolete("Subscribe to new ProjectLoaded event.")]
        public static event Action ManifestUpdated;
        public static event Action ProjectLoaded;
        private static ManifestScheme nextManifestScheme;

        public static ManifestScheme LoadedManifestScheme
        {
            get => LatestProject.Manifest;
            private set
            {
                if (LatestProject.Manifest != null && ReferenceEquals(LatestProject.Manifest, value))
                {
                    return;
                }
                LatestProject.Manifest = value;
            
                ManifestUpdated?.Invoke();
            }
        }
        public static bool IsManifestLoadInProgress => nextManifestScheme != null;

        // public static string ProjectPath
        // {
        //     get => LatestProject.ProjectPath;
        //     set => LatestProject.ProjectPath = value;
        // }
        //
        // public static string DefaultContentPath => Path.Combine(ProjectPath, DefaultContentDirectory);
        public static string DefaultContentDirectory = "Content";
        
        public static string GetDefaultContentPath(string projectPath, string schemeName)
        {
            return Path.Combine(projectPath, DefaultContentDirectory, schemeName);
        }
        
        public static string GetDefaultContentPath(SchemaContext ctx, string schemeName)
        {
            return Path.Combine(ctx.Project.DefaultContentPath, schemeName);
        }

        public static string GetContentPath(string schemeFileName)
        {
            return Path.Combine(LatestProject.DefaultContentPath, schemeFileName);
        }
        
        public static string ContentLoadPath => LatestProject.DefaultContentPath;
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
        public static SchemaResult<SchemaProjectContainer> InitializeTemplateManifestScheme(SchemaContext context,
            string projectPath,
            string defaultScriptExportPath)
        {
            var res = SchemaResult<SchemaProjectContainer>.New(context);
            if (GetStorage(context).TryErr(out var storage, out var storageErr)) return storageErr.CastError(res);
            
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
                        return createRes.CastError(res);
                    }
                }
                
                // Prime the manifest as loaded before validation to avoid FS path checks requiring initialization
                var manifestScheme = new ManifestScheme(templateManifestScheme._);
                var emptyProjectContainer = new SchemaProjectContainer();
                // context.Project = emptyProjectContainer; // setting up empty project..
                using var _ = new ProjectContextScope(ref context, emptyProjectContainer);
                emptyProjectContainer.Manifest = manifestScheme;
                emptyProjectContainer.ProjectPath = projectPath;
                var result = LoadDataScheme(context, templateManifestScheme._, overwriteExisting: true);
                Logger.LogDbgVerbose(result.Message, result.Context);
                if (result.Passed)
                {
                    IsTemplateManifestLoaded = true;
                    LatestProject = emptyProjectContainer;
                    return res.Pass(emptyProjectContainer, "Loaded Template Manifest");
                }

                return result.CastError(res);
            }
        }
        #endregion

        /// <summary>
        /// Gets the manifest scheme if loaded and initialized.
        /// </summary>
        /// <returns>A <see cref="SchemaResult{DataScheme}"/> indicating success or failure, and the manifest scheme if successful.</returns>
        public static SchemaResult<ManifestScheme> GetManifestScheme(SchemaContext context)
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

            if (LoadedManifestScheme != null)
            {
                return res.Pass(LoadedManifestScheme, "Manifest scheme is already loaded");
            }

            if (!GetScheme(context, Manifest.MANIFEST_SCHEME_NAME).Try(out var manifest, 
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
        /// <param name="context"></param>
        /// <param name="scheme">The data scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        public static SchemaResult<ManifestEntry> GetManifestEntryForScheme(SchemaContext context, DataScheme scheme)
        {
            var res = SchemaResult<ManifestEntry>.New(context);
            
            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<ManifestEntry>();
            }
            
            return GetManifestEntryForScheme(context, scheme.SchemeName);
        }

        /// <summary>
        /// Gets the manifest entry for a given scheme name.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="schemeName">The name of the scheme to look up in the manifest.</param>
        /// <returns>A <see cref="SchemaResult{DataEntry}"/> indicating success or failure, and the manifest entry if successful.</returns>
        public static SchemaResult<ManifestEntry> GetManifestEntryForScheme(SchemaContext ctx,
            string schemeName)
        {
            var res = SchemaResult<ManifestEntry>.New(schemeName);

            var isInitRes = IsInitialized(ctx);
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
                if (!GetManifestScheme(ctx).Try(out var manifestScheme, out var manifestError))
                    return manifestError.CastError<ManifestEntry>();
                
                return manifestScheme.GetEntryForSchemeName(ctx, schemeName);

            }
        }
    }
}