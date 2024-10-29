using System.Collections.Generic;
using Schema.Core;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class StorageUtil
    {
        public static string DefaultContentDirectory = "Content";
        
        public static IStorageFormat<DataScheme> JSONStorageFormat = new JsonStorageFormat<DataScheme>();
        public static IStorageFormat<DataScheme> CSVStorageFormat = new CSVStorageFormat();

        public static IEnumerable<IStorageFormat<DataScheme>> AllFormats = new[]
        {
            JSONStorageFormat,
            CSVStorageFormat,
        };
        
        public static void Export(this IStorageFormat<DataScheme> format, DataScheme schema)
        {
            string filePath = EditorUtility.SaveFilePanel($"Save {format.Extension.ToUpper()}", DefaultContentDirectory, 
                $"{schema.SchemaName}.{format.Extension}", 
                format.Extension);
            
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("Export canceled, no file path provided.");
                return;
            }
            
            format.Save(filePath, schema);

            Debug.Log($"Schema \"{schema.SchemaName}\" exported successfully to {filePath}");
        }

        public static bool TryImport(this IStorageFormat<DataScheme> format, out DataScheme schema)
        {
            string filePath = EditorUtility.OpenFilePanel($"Import from {format.Extension.ToUpper()}", DefaultContentDirectory, format.Extension);

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("Import canceled, no file path provided.");
                schema = null;
                return false;
            }
            Debug.Log($"Importing scheme from file: {filePath}");

            schema = format.Load(filePath);
            return true;
        }
    }
}