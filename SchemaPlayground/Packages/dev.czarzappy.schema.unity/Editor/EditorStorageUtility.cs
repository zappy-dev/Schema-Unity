using System.IO;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class EditorStorageUtility
    {
        public static string DefaultContentDirectory = "Content";
        
        public static void Export(this IStorageFormat<DataScheme> format, DataScheme scheme)
        {
            var extParts = format.Extension.Split('.');
            var lastExt = extParts[extParts.Length - 1];
            var exportFileName = $"{scheme.SchemeName}Scheme.{format.Extension}";
            Debug.Log($"Export {exportFileName}");
            Debug.Log($"lastExt {lastExt}");
            string saveDirectory = (format is CSharpStorageFormat) ? "Packages/dev.czarzappy.schema.unity/Core/Schemes" : DefaultContentDirectory;

            string outputFilePath;
            if (format is CSharpStorageFormat)
            {
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
                Debug.LogWarning("Export canceled, no file path provided.");
                return;
            }
            
            format.SerializeToFile(outputFilePath, scheme);

            Debug.Log($"Schema \"{scheme.SchemeName}\" exported successfully to {outputFilePath}");

            AssetDatabase.Refresh();
        }

        public static bool TryImport(this IStorageFormat<DataScheme> format, out DataScheme scheme, out string importFilePath)
        {
            importFilePath = EditorUtility.OpenFilePanel($"Import from {format.Extension.ToUpper()}", DefaultContentDirectory, format.Extension);

            if (string.IsNullOrEmpty(importFilePath))
            {
                Debug.LogWarning("Import canceled, no file path provided.");
                scheme = null;
                return false;
            }
            Debug.Log($"Importing scheme from file: {importFilePath}");

            return format.DeserializeFromFile(importFilePath).Try(out scheme);
        }
    }
}