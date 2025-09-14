using System;
using System.IO;
using Schema.Core;
using Schema.Core.IO;
using UnityEngine;

namespace Schema.Runtime.IO
{
    public class TextAssetResourcesFileSystem : IFileSystem
    {
        private const string RESOURCES_FOLDER_NAME = "Resources";
        
        private static SchemaResult<string> SanitizeResourcePath(SchemaContext context, string path)
        {
            var res = SchemaResult<string>.New(context);
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
            // Also do not use folder paths if present, only the file name

            resourcePath = PathUtility.SanitizePath(resourcePath);
            resourcePath = Path.GetFileNameWithoutExtension(resourcePath);
            
            return res.Pass(resourcePath);
        }
        public SchemaResult<string> ReadAllText(SchemaContext context, string filePath)
        {
            if (!LoadTextAsset(context, filePath).Try(out var textAsset, out var error))
            {
                return error.CastError<string>();
            }

            return SchemaResult<string>.Pass(textAsset.text);
        }

        public SchemaResult<string[]> ReadAllLines(SchemaContext context, string filePath)
        {
            if (!LoadTextAsset(context, filePath).Try(out var textAsset, out var error))
            {
                return error.CastError<string[]>();
            }

            var rows = textAsset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            return SchemaResult<string[]>.Pass(rows);
        }

        public SchemaResult WriteAllText(SchemaContext context, string filePath, string fileContent)
        {
            throw new InvalidOperationException("Cannot write text file in Resources folder.");
        }

        public SchemaResult FileExists(SchemaContext context, string filePath)
        {
            return LoadTextAsset(context, filePath).Cast();
        }

        private SchemaResult<TextAsset> LoadTextAsset(SchemaContext context, string filePath)
        {
            if (!SanitizeResourcePath(context, filePath).Try(out var sanitizedPath, out var error))
            {
                return error.CastError<TextAsset>();
            }

            try
            {
                var textAsset = Resources.Load<TextAsset>(sanitizedPath);
                return SchemaResult<TextAsset>.CheckIf(textAsset == true, textAsset, $"{sanitizedPath} does not exist in Resources folder.", context: context);
            }
            catch (Exception ex)
            {
                return SchemaResult<TextAsset>.Fail($"{sanitizedPath} does not exist in Resources folder.", context);
            }
        }

        public bool DirectoryExists(SchemaContext context, string directoryPath)
        {
            throw new InvalidOperationException("Directory does not exist in Resources folder.");
        }

        public SchemaResult CreateDirectory(SchemaContext context, string directoryPath)
        {
            throw new InvalidOperationException("Unable to create directory in Resources folder.");
        }
    }
}