using System.Collections.Generic;
using Schema.Core;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class EditorStorageUtility
    {
        public static string DefaultContentDirectory = "Content";
        
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

        public static bool TryImport(this IStorageFormat<DataScheme> format, out DataScheme schema, out string importFilePath)
        {
            importFilePath = EditorUtility.OpenFilePanel($"Import from {format.Extension.ToUpper()}", DefaultContentDirectory, format.Extension);

            if (string.IsNullOrEmpty(importFilePath))
            {
                Debug.LogWarning("Import canceled, no file path provided.");
                schema = null;
                return false;
            }
            Debug.Log($"Importing scheme from file: {importFilePath}");

            schema = format.Load(importFilePath);
            return true;
        }
    }
}