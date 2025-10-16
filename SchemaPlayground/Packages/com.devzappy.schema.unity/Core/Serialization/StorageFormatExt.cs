using System.IO;
using Schema.Core.Data;
using Schema.Core.Logging;

namespace Schema.Core.Serialization
{
    public static class StorageFormatExt
    {
        #region Static API
        public delegate string ResolveExportPath(ISchemeStorageFormat format, string exportFileName);
        
        public static SchemaResult Export(this ISchemeStorageFormat format, DataScheme scheme, SchemaContext context, ResolveExportPath resolveExportPath)
        {
            var sanitizedBaseFileName = scheme.SchemeName
                .Replace(" ", string.Empty);
            var exportFileName = $"{sanitizedBaseFileName}Scheme.{format.Extension}";
            
            if (!Schema.GetManifestEntryForScheme(scheme).Try(out var manifestEntry, out var manifestError))
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
                return SchemaResult.Fail(context, "Export canceled, no file path provided.");
            }
            
            var serializeRes = format.SerializeToFile(context, outputFilePath, scheme);
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