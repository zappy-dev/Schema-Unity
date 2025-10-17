using System.Threading;
using System.Threading.Tasks;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Serialization;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class EditorStorageUtility
    {
        public static async Task<(bool, DataScheme scheme, string importFilePath)> TryImport(this ISchemeStorageFormat format, 
            SchemaContext context,
            CancellationToken cancellationToken)
        {
            await EditorMainThread.Switch(cancellationToken);
            var importFilePath = EditorUtility.OpenFilePanel($"Import from {format.Extension.ToUpper()}", Schema.Core.Schema.DefaultContentDirectory, format.Extension);

            DataScheme scheme;
            if (string.IsNullOrEmpty(importFilePath))
            {
                Debug.LogWarning("Import canceled, no file path provided.");
                return (false, null, importFilePath);
            }
            Debug.Log($"Importing scheme from file: {importFilePath}");

            var importRes = await format.DeserializeFromFile(context, importFilePath, cancellationToken);
            
            return (importRes.Try(out scheme), scheme, importFilePath);
        }
    }
}