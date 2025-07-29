using System;
using System.IO;
using Schema.Core.Data;
using Schema.Core.IO;
using Logger = Schema.Core.Logging.Logger;
#if UNITY_EDITOR
using Schema.Core.Logging;
using UnityEngine;
using UnityEditor;
#endif

namespace Schema.Core.Serialization
{
    /// <summary>
    /// Extension methods for IStorageFormat to provide additional functionality
    /// </summary>
    public static class StorageFormatExtensions
    {
        /// <summary>
        /// Tries to import a schema from a file selected by the user
        /// </summary>
        /// <param name="storageFormat">The storage format to use for import</param>
        /// <param name="importedSchema">The imported schema, if successful</param>
        /// <param name="importFilePath">The path to the imported file, if successful</param>
        /// <returns>True if import was successful, false otherwise</returns>
        public static bool TryImport(this IStorageFormat<DataScheme> storageFormat, out DataScheme importedSchema, out string importFilePath)
        {
            importedSchema = null;
            importFilePath = null;
            
#if UNITY_EDITOR
            // Use Unity's file dialog to select a file
            string filter = $"{storageFormat.Extension.ToUpper()} Files (*.{storageFormat.Extension})|*.{storageFormat.Extension}";
            string path = EditorUtility.OpenFilePanel($"Import {storageFormat.Extension.ToUpper()} Schema", "", storageFormat.Extension);
            
            if (string.IsNullOrEmpty(path))
                return false;
                
            try
            {
                // Get the content base path
                string contentBasePath = null;
                if (Schema.IsInitialized && !string.IsNullOrEmpty(Schema.ManifestImportPath))
                {
                    contentBasePath = Schema.ContentLoadPath;
                }
                
                // Try to deserialize the file
                var result = storageFormat.DeserializeFromFile(path);
                if (!result.Try(out importedSchema))
                    return false;
                    
                // Convert to relative path if possible
                if (!string.IsNullOrEmpty(contentBasePath))
                {
                    importFilePath = PathUtility.MakeRelativePath(path, contentBasePath);
                    
                    // If the path couldn't be made relative (e.g., different drive), use the absolute path
                    if (importFilePath == path)
                    {
                        importFilePath = path;
                    }
                }
                else
                {
                    importFilePath = path;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error importing schema: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Exports a schema to a file selected by the user
        /// </summary>
        /// <param name="storageFormat">The storage format to use for export</param>
        /// <param name="scheme">The schema to export</param>
        /// <returns>True if export was successful, false otherwise</returns>
        public static bool Export(this IStorageFormat<DataScheme> storageFormat, DataScheme scheme)
        {
#if UNITY_EDITOR
            // Use Unity's file dialog to select a save location
            string defaultName = $"{scheme.SchemeName}.{storageFormat.Extension}";
            string path = EditorUtility.SaveFilePanel($"Export {storageFormat.Extension.ToUpper()} Schema", 
                "", defaultName, storageFormat.Extension);
                
            if (string.IsNullOrEmpty(path))
                return false;
                
            try
            {
                // Try to serialize the schema to the selected file
                var result = storageFormat.SerializeToFile(path, scheme);
                return result.Passed;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error exporting schema: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }
    }
}