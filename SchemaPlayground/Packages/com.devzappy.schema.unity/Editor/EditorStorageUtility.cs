using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class EditorStorageUtility
    {

        public static bool TryImport(this ISchemeStorageFormat format, SchemaContext context, out DataScheme scheme, out string importFilePath)
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