using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Logging;
using static Schema.Core.SchemaResult;

namespace Schema.Core.IO
{
    public class LocalFileSystem : IFileSystem
    {
        #region File Operations
        public Task<SchemaResult<string>> ReadAllText(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            var res = SchemaResult<string>.New(context);
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            if (!File.Exists(sanitizedPath))
            {
                return Task.FromResult(res.Fail("File not found: " + sanitizedPath));
            }
            
            return Task.FromResult(res.Pass(File.ReadAllText(sanitizedPath)));
        }

        public Task<SchemaResult<string[]>> ReadAllLines(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            return Task.FromResult(SchemaResult<string[]>.Pass(File.ReadAllLines(sanitizedPath)));
        }

        public async Task<SchemaResult> WriteAllText(SchemaContext context, string filePath, string fileContent, CancellationToken cancellationToken = default)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            Logger.LogDbgVerbose($"Writing file {sanitizedPath}, size: {fileContent.Length}");
            
            // Extract the directory path from the file path
            var directoryPath = Path.GetDirectoryName(sanitizedPath);

            // Check if the directory exists, and if not, create it
            var dirExists = await DirectoryExists(context, directoryPath, cancellationToken);
            if (!dirExists)
            {
                var createDirRes = await CreateDirectory(context, directoryPath, cancellationToken);

                if (createDirRes.Failed)
                {
                    return Fail(context, "Failed to create directory");
                }
            }
            File.WriteAllText(sanitizedPath, fileContent);
            return Pass();
        }

        public Task<SchemaResult> FileExists(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            var sanitizedPath = PathUtility.SanitizePath(filePath);
            
            return Task.FromResult(CheckIf(context, File.Exists(sanitizedPath), $"File '{sanitizedPath}' does not exist"));
        }
        
        #endregion
        
        #region Directory Operations
        
        public Task<bool> DirectoryExists(SchemaContext context, string directoryPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(false);
            }
            var sanitizedPath = PathUtility.SanitizePath(directoryPath);
            
            return Task.FromResult(Directory.Exists(sanitizedPath));
        }

        public async Task<SchemaResult> CreateDirectory(SchemaContext context, string directoryPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Fail(context, "Directory is empty");
            }
            var sanitizedPath = PathUtility.SanitizePath(directoryPath);

            // Directory already exists, move on
            bool doesExist = await DirectoryExists(context, sanitizedPath, cancellationToken);
            if (doesExist)
            {
                return Pass("Directory already exists");
            }
            
            Directory.CreateDirectory(sanitizedPath);
            
            return Pass();
        }
        
        #endregion
    }
}