using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Schemes;
using Schema.Core.Serialization;
using static Schema.Core.SchemaResult;

namespace Schema.Core
{
    public static partial class Schema
    {
        #region Constants
        public static string DEFAULT_PUBLISH_PATH = Path.Combine("Assets", "Plugins", "Schema");
        public static string DEFAULT_RESOURCE_PUBLISH_PATH = Path.Combine(DEFAULT_PUBLISH_PATH, "Resources");
        public static string DEFAULT_SCRIPTS_PUBLISH_PATH = Path.Combine("Assets", "Scripts", "Schemes");
        
        #endregion
        
        public delegate void PublishOperation(SchemaContext ctx, DataScheme schemeToPublish);
        
        public class PublishConfig
        {
            public PublishOperation PostPublish { get; set; }

            public StorageFormatExt.ResolveExportPath ResolveExportPath { get; set; }
            public Storage PublishStorage { get; set; }
            public bool RunCodeGen { get; }
        }
        
        public static async Task<SchemaResult> PublishAllSchemes(SchemaContext ctx, 
            PublishConfig publishConfig, 
            IProgress<(float, string)> progress = null,
            CancellationToken cancellationToken = default)
        {
            // get all loaded schemes and publish in topological order.
            if (!DataScheme.TopologicalSortByReferences(ctx, ctx.Project.Schemes.Values)
                    .Try(out var schemesToPublish, out var sortErr))
            {
                return sortErr.Cast();
            }
            
            return await BulkPublishSchemes(ctx, publishConfig, schemesToPublish.Select(s => s.SchemeName).ToList(), progress, cancellationToken);
        }
        
        public static async Task<SchemaResult> BulkPublishSchemes(SchemaContext context, 
            PublishConfig publishConfig, 
            IReadOnlyList<string> schemeNames, 
            IProgress<(float, string)> progress = null,
            CancellationToken cancellationToken = default)
        {
            // using var progressScope = new ProgressScope("Schema - Publish All");
            // TODO: need a way of generating an aggregate schema result
            int numPublished = 1;
            
            int total = schemeNames.Count;

            var bulkRes = await BulkResult(
                entries: schemeNames,
                haltOnError: true, // Avoid publishing bad code if a Scheme fails to publish
                operation: async (schemeName) =>
                {
                    progress?.Report((numPublished * 1.0f / total, $"Publishing Scheme '{schemeName}' ({numPublished} / {total})"));
                    numPublished++;
                    return await PublishScheme(context, publishConfig, schemeName, cancellationToken);
                },
                errorMessage: "Failed to publish all schemes. Check console logs for more details.");
            
            return bulkRes;
        }
        
        
        public static async Task<SchemaResult> PublishScheme(SchemaContext ctx, PublishConfig publishConfig, string schemeName, CancellationToken cancellationToken = default)
        {
            Logger.LogDbgVerbose($"Publishing {schemeName}");
            var schemeEntry = GetManifestEntryForScheme(ctx, schemeName);
            if (!schemeEntry.Try(out ManifestEntry manifestEntry) ||
                !GetScheme(ctx, schemeName).Try(out var schemeToPublish))
            {
                return Fail(ctx, $"Could not find manifest entry for scheme to publish, scheme: {schemeName}");
            }

            // TODO: Figure out how to better enumerate this, enforce Manifest scheme attribute is valid
            // Problem: Manifest Scheme loads first, before any other scheme, so if it has a reference data type attribute to another scheme, that is invalid
            // could skip validation and validate after load?
            // But seems hacky
            bool hasValidPublishTarget = Enum.TryParse<ManifestScheme.PublishTarget>(manifestEntry.PublishTarget, 
                out var publishTarget);

            if (!hasValidPublishTarget)
            {
                return Fail(ctx, $"Invalid publish target for scheme to publish, scheme: {schemeName}, found target: {manifestEntry.PublishTarget}, " +
                            $"expected a valid type from {nameof(ManifestScheme.PublishTarget)}.");
            }

            if (!IsValidScheme(ctx, schemeToPublish).Try(out var validationErr))
            {
                return validationErr;
            }
            
            // publishing data
            bool isSuccess = true;
            switch (publishTarget)
            {
                // TODO: Re-store Scriptable Object publishing
                // case ManifestScheme.PublishTarget.SCRIPTABLE_OBJECT:
                // {
                //     // HACK: Assumes ID column is the asset guid for an underlying scriptable object to publish to
                //     if (!schemeToPublish.GetIdentifierAttribute().Try(out var idAttr))
                //     {
                //         return Fail(context, "No identifier attribute found for scheme to publish");
                //     }
                //     
                //     foreach (var entry in schemeToPublish.AllEntries)
                //     {
                //         var publishRes = PublishEntryToScriptableObject(context, schemeToPublish, entry, idAttr);
                //         isSuccess &= publishRes.Passed;
                //     }
                //     break;
                // }
                case ManifestScheme.PublishTarget.RESOURCES:
                {
                    var publishRes = await PublishToResources(ctx, publishConfig, manifestEntry, schemeToPublish, cancellationToken);
                    isSuccess = publishRes.Passed;
                }
                    break;
            }
            
            if (!isSuccess) return Fail(ctx, "Publishing scheme failed.");

            if (publishConfig.RunCodeGen)
            {
                // TODO: Define a separate publishing storage for codegen?
                // How should Web API publish code?
                if (!GetStorage(ctx).Try(out var storage, out var storageError)) return storageError.Cast();
                
                // Publish code
                var exportRes = await storage.CSharpSchemeStorageFormat.Export(schemeToPublish, ctx, publishConfig.ResolveExportPath);
                if (exportRes.Failed)
                {
                    return exportRes;
                }
            }
            
            publishConfig?.PostPublish?.Invoke(ctx, schemeToPublish);
            
            return Pass("Published assets");
        }
        

        /// <summary>
        /// Publishes a given scheme to an associated Resources file
        /// </summary>
        /// <param name="context"></param>
        /// <param name="publishConfig"></param>
        /// <param name="originalSchemeToPublish"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task<SchemaResult> PublishToResources(SchemaContext context, PublishConfig publishConfig,
            ManifestEntry manifestEntry,
            DataScheme originalSchemeToPublish, CancellationToken cancellationToken)
        {
            // if (!GetStorage(context).Try(out var storage, out var storageError)) return storageError.Cast();

            var storage = publishConfig.PublishStorage;
            
            var storageFormat = storage.DefaultSchemaPublishFormat;
            // string schemaPublishPath = $"{DEFAULT_RESOURCE_PUBLISH_PATH}/{storageFormat.ResolveFileName(originalSchemeToPublish.SchemeName)}";
            string schemaPublishPath = $"{DEFAULT_RESOURCE_PUBLISH_PATH}/{manifestEntry.FilePath}";
            schemaPublishPath = RemoteFileSystem.SanitizePath(schemaPublishPath);
            // string schemaPublishPath = storageFormat.ResolvePublishPath(originalSchemeToPublish.SchemeName);

            // prepare a new data scheme to publish
            var publishScheme = new DataScheme(originalSchemeToPublish.SchemeName);

            // first clone published attributes
            var publishedAttributes = originalSchemeToPublish.GetAttributes(a => a.ShouldPublish).ToList();
            foreach (var publishedAttr in publishedAttributes)
            {
                publishScheme.AddAttribute(context, publishedAttr.Clone() as AttributeDefinition);
            }
            
            // second clone data entries, stripped for only published entries
            if (!originalSchemeToPublish.GetEntries(context: context).Try(out var entries, out var error)) return error.Cast();
            
            foreach (var entry in entries)
            {
                var publishedEntry = new DataEntry();

                var copyRes = BulkResult(
                    entries: publishedAttributes,
                    operation: (publishedAttr) =>
                        publishedEntry.SetData(context, publishedAttr.AttributeName, entry.GetDataDirect(publishedAttr)),
                    errorMessage: $"Failed to publish entry: {entry}",
                    context: context);

                if (copyRes.Failed)
                {
                    return copyRes;
                }

                publishScheme.AddEntry(context, publishedEntry);
            }
            
            var publishRes = await storageFormat.SerializeToFile(context, schemaPublishPath, publishScheme, cancellationToken);

            return publishRes;
        }
    }
}