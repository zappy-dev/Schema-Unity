using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;
using Schema.Core.Logging;

namespace Schema.Core.Serialization
{
    public static class StorageFormatExt
    {
        #region Static API

        public static string ResolveFileName(this ISchemeStorageFormat storageFormat, string schemeName)
        {
            return $"{schemeName}.{storageFormat.Extension}";
        }

        public static string ResolvePublishPath(this ISchemeStorageFormat storageFormat, string schemeName)
        {
            return $"{Schema.DEFAULT_RESOURCE_PUBLISH_PATH}/{storageFormat.ResolveFileName(schemeName)}";
        }
        
        public delegate string ResolveExportPath(ISchemeStorageFormat format, string exportFileName);
        
        public static async Task<SchemaResult> Export(this ISchemeStorageFormat format, DataScheme scheme, SchemaContext ctx, ResolveExportPath resolveExportPath, CancellationToken cancellationToken = default)
        {
            var sanitizedBaseFileName = scheme.SchemeName
                .Replace(" ", string.Empty);
            var exportFileName = $"{sanitizedBaseFileName}Scheme.{format.Extension}";
            
            if (!Schema.GetManifestEntryForScheme(ctx, scheme).Try(out var manifestEntry, out var manifestError))
            {
                return manifestError.Cast();
            }
            
            string saveDirectory = (format is CSharpSchemeStorageFormat) ? 
                manifestEntry.CSharpExportPath : 
                Schema.DefaultContentDirectory;

            string outputFilePath;
            if (format is CSharpSchemeStorageFormat cSharpStorageFormat)
            {
                cSharpStorageFormat.ManifestEntry = manifestEntry;
                exportFileName = exportFileName.Substring(0, 1).ToUpper() +  exportFileName.Substring(1);
                outputFilePath = Path.Combine(saveDirectory, exportFileName);
            }
            else
            {
                outputFilePath = resolveExportPath?.Invoke(format, exportFileName);
            }
            
            if (string.IsNullOrEmpty(outputFilePath))
            {
                return SchemaResult.Fail(ctx, "Export canceled, no file path provided.");
            }
            
            var serializeRes = await format.SerializeToFile(ctx, outputFilePath, scheme, cancellationToken);
            if (serializeRes.Failed)
            {
                return serializeRes;
            }

            Logger.Log($"Schema \"{scheme.SchemeName}\" exported successfully to {outputFilePath}");

            return SchemaResult.Pass($"Schema '{scheme.SchemeName}' exported to {outputFilePath}");
        }

        #endregion
    }
}