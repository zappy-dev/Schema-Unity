using System;
using System.IO;
using Schema.Core.Logging;
using static Schema.Core.SchemaResult;

namespace Schema.Core.IO
{
    public class LocalFileSystem : IFileSystem
    {
        #region File Operations
        public SchemaResult<string> ReadAllText(SchemaContext context, string filePath)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            return SchemaResult<string>.Pass(File.ReadAllText(sanitizedPath));
        }

        public SchemaResult<string[]> ReadAllLines(SchemaContext context, string filePath)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            return SchemaResult<string[]>.Pass(File.ReadAllLines(sanitizedPath));
        }

        public SchemaResult WriteAllText(SchemaContext context, string filePath, string fileContent)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            Logger.LogDbgVerbose($"Writing file {sanitizedPath}, size: {fileContent.Length}");
            
            // Extract the directory path from the file path
            var directoryPath = Path.GetDirectoryName(sanitizedPath);

            // Check if the directory exists, and if not, create it
            var dirExists = DirectoryExists(context, directoryPath);
            if (!dirExists)
            {
                var createDirRes = CreateDirectory(context, directoryPath);

                if (createDirRes.Failed)
                {
                    return Fail(context, "Failed to create directory");
                }
            }
            File.WriteAllText(sanitizedPath, fileContent);
            return Pass();
        }

        public SchemaResult FileExists(SchemaContext context, string filePath)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            
            return CheckIf(context, File.Exists(sanitizedPath), $"File '{sanitizedPath}' does not exist");
        }
        
        #endregion
        
        #region Directory Operations
        
        public bool DirectoryExists(SchemaContext context, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }
            var sanitizedPath = PathUtility.SanitizePath(directoryPath);
            
            return Directory.Exists(sanitizedPath);
        }

        public SchemaResult CreateDirectory(SchemaContext context, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Fail(context, "Directory is empty");
            }
            var sanitizedPath = PathUtility.SanitizePath(directoryPath);

            // Directory already exists, move on
            if (DirectoryExists(context, sanitizedPath))
            {
                return Pass("Directory already exists");
            }
            
            Directory.CreateDirectory(sanitizedPath);
            
            return Pass();
        }
        
        #endregion
    }
}