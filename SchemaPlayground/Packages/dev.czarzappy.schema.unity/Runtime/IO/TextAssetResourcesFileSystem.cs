using System;
using System.IO;
using Schema.Core;
using Schema.Core.IO;
using UnityEngine;

namespace Schema.Runtime.IO
{
    public class TextAssetResourcesFileSystem : IFileSystem
    {
        private static string Context = nameof(TextAssetResourcesFileSystem);
        private const string RESOURCES_FOLDER_NAME = "Resources";
        
        private static SchemaResult<string> SanitizeResourcePath(string path)
        {
            var res = SchemaResult<string>.New(Context);
            if (PathUtility.IsAbsolutePath(path))
            {
                return res.Fail("Cannot use absolute paths for Resources files.");
            }

            string resourcePath = path;
            // extract path under Resources/ folder
            var resourcesPathIdx = resourcePath.IndexOf(RESOURCES_FOLDER_NAME, StringComparison.Ordinal);
            if (resourcesPathIdx > -1)
            {
                resourcePath = resourcePath.Substring(resourcesPathIdx +  RESOURCES_FOLDER_NAME.Length);
            }

            // remove extensions
            if (Path.HasExtension(resourcePath))
            {
                var ext = Path.GetExtension(resourcePath);
                resourcePath = resourcePath.Substring(0, resourcePath.Length - ext.Length);
            }
            
            return res.Pass(resourcePath, Context);
        }
        public SchemaResult<string> ReadAllText(string filePath)
        {
            if (!LoadTextAsset(filePath).Try(out var textAsset, out var error))
            {
                return error.CastError<string>();
            }

            return SchemaResult<string>.Pass(textAsset.text);
        }

        public SchemaResult<string[]> ReadAllLines(string filePath)
        {
            if (!LoadTextAsset(filePath).Try(out var textAsset, out var error))
            {
                return error.CastError<string[]>();
            }

            var rows = textAsset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            return SchemaResult<string[]>.Pass(rows);
        }

        public SchemaResult WriteAllText(string filePath, string fileContent)
        {
            throw new System.NotImplementedException("Writing to files is not allowed for Resources files.");
        }

        public SchemaResult FileExists(string filePath)
        {
            return LoadTextAsset(filePath).Cast();
        }

        private SchemaResult<TextAsset> LoadTextAsset(string filePath)
        {
            if (!SanitizeResourcePath(filePath).Try(out var sanitizedPath, out var error))
            {
                return error.CastError<TextAsset>();
            }

            try
            {
                var textAsset = Resources.Load<TextAsset>(sanitizedPath);
                return SchemaResult<TextAsset>.CheckIf(textAsset == true, textAsset, $"{sanitizedPath} does not exist in Resources folder.", context: Context);
            }
            catch (Exception ex)
            {
                return SchemaResult<TextAsset>.Fail($"{sanitizedPath} does not exist in Resources folder.", Context);
            }
        }

        public SchemaResult DirectoryExists(string directoryPath)
        {
            throw new System.NotImplementedException("Unable to access folder information from Resources");
        }

        public SchemaResult CreateDirectory(string directoryPath)
        {
            throw new System.NotImplementedException("Creating a Resources directory during Runtime is not allowed");
        }
    }
}