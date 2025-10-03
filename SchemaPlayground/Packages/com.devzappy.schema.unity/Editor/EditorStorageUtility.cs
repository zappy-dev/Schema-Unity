using System.IO;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Ext;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public static class EditorStorageUtility
    {
        public static SchemaResult Export(this IStorageFormat<DataScheme> format, DataScheme scheme, SchemaContext context)
        {
            var extParts = format.Extension.Split('.');
            var lastExt = extParts[extParts.Length - 1];
            var sanitizedBaseFileName = scheme.SchemeName
                .Replace(" ", string.Empty);
            var exportFileName = $"{sanitizedBaseFileName}Scheme.{format.Extension}";
            Logger.LogDbgVerbose($"Export {exportFileName}, last ext: {lastExt}");
            
            if (!Core.Schema.GetManifestEntryForScheme(scheme).Try(out var manifestEntry, out var manifestError))
            {
                return manifestError.Cast();
            }
            
            // TODO: Set a different target directory preference for csharp code?
            // string saveDirectory = (format is CSharpStorageFormat) ? "Packages/dev.czarzappy.schema.unity/Runtime/Schemes" : DefaultContentDirectory;
            // string saveDirectory = (format is CSharpStorageFormat) ? 
            //     "Packages/dev.czarzappy.schema.unity/Core/Schemes" : 
            //     DefaultContentDirectory;
            string saveDirectory = (format is CSharpStorageFormat) ? 
                manifestEntry.CSharpExportPath : 
                Schema.Core.Schema.DefaultContentDirectory;

            string outputFilePath;
            if (format is CSharpStorageFormat cSharpStorageFormat)
            {
                cSharpStorageFormat.ManifestEntry = manifestEntry;
                exportFileName = exportFileName.Substring(0, 1).ToUpper() +  exportFileName.Substring(1);
                outputFilePath = Path.Combine(saveDirectory, exportFileName);
            }
            else
            {
                outputFilePath = EditorUtility.SaveFilePanel($"Save {format.Extension.ToUpper()}", saveDirectory, 
                    exportFileName, 
                    lastExt);
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

            Debug.Log($"Schema \"{scheme.SchemeName}\" exported successfully to {outputFilePath}");

            AssetDatabase.Refresh();
            return SchemaResult.Pass($"Schema '{scheme.SchemeName}' exported to {outputFilePath}");
        }

        public static bool TryImport(this IStorageFormat<DataScheme> format, SchemaContext context, out DataScheme scheme, out string importFilePath)
        {
            importFilePath = EditorUtility.OpenFilePanel($"Import from {format.Extension.ToUpper()}", Schema.Core.Schema.DefaultContentDirectory, format.Extension);

            if (string.IsNullOrEmpty(importFilePath))
            {
                Debug.LogWarning("Import canceled, no file path provided.");
                scheme = null;
                return false;
            }
            Debug.Log($"Importing scheme from file: {importFilePath}");

            return format.DeserializeFromFile(context, importFilePath).Try(out scheme);
        }
    }
}