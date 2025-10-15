using System;
using Schema.Core;
using Schema.Core.Ext;
using Schema.Core.IO;
using UnityEngine;

namespace Schema.Runtime.IO
{
    public class TextAssetResourcesFileSystem : IFileSystem
    {
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

            var rows = textAsset.text.SplitByLineEndings();
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
            if (!ResourcesUtils.SanitizeResourcePath(context, filePath).Try(out var sanitizedPath, out var error))
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
                return SchemaResult<TextAsset>.Fail($"{sanitizedPath} does not exist in Resources folder, reason: {ex.Message}", context);
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