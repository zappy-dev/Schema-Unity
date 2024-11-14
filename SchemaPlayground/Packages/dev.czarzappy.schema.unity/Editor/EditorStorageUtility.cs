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
            string filePath = EditorUtility.SaveFilePanel($"Save {format.Extension.ToUpper()}", DefaultContentDirectory, 
                $"{scheme.SchemeName}.{format.Extension}", 
                format.Extension);
            
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("Export canceled, no file path provided.");
                return;
            }
            
            format.SerializeToFile(filePath, scheme);

            Debug.Log($"Schema \"{scheme.SchemeName}\" exported successfully to {filePath}");
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

            return format.TryDeserializeFromFile(importFilePath, out scheme);
        }
    }
}