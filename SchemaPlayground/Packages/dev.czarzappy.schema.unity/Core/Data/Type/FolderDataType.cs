using Newtonsoft.Json;
using Schema.Core.IO;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    public class FolderDataType : FSDataType
    {
        public override SchemaContext Context => new SchemaContext
        {
            DataType = nameof(FolderDataType)
        };

        public override string TypeName => "Folder";
        
        public FolderDataType(bool allowEmptyPath = true, bool useRelativePaths = false, string basePath = null) 
            : base(allowEmptyPath, useRelativePaths, basePath)
        {
        }
        
        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            if (!(value is string filePath))
            {
                return Fail("Value is not a file path", context);
            }
            
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return CheckIf(allowEmptyPath, errorMessage: "File path is empty", successMessage: "File path is set", context);
            }

            if (PathUtility.IsAbsolutePath(filePath) && useRelativePaths)
            {
                return Fail("File path is absolute when only relative paths are supported", context);
            }
            
            // Resolve the path to absolute for file system check
            string resolvedPath = ResolvePath(filePath);
            
            return Schema.Storage.FileSystem.DirectoryExists(resolvedPath);
        }

        public override SchemaResult<object> ConvertData(object value, SchemaContext context)
        {
            if (!(value is string filePath))
            {
                return Fail<object>($"Value '{value}' is not a file path", context: context);
            }
            
            // Format the path according to our settings (relative/absolute)
            string formattedPath = FormatPath(filePath);
            Logger.LogDbgVerbose($"Formatted path: {formattedPath}");
            
            // For validation, we need to resolve to absolute path
            string resolvedPath = ResolvePath(filePath);
            Logger.LogDbgVerbose($"Resolved path: {resolvedPath}");
            
            bool directoryExists = !string.IsNullOrWhiteSpace(resolvedPath) && 
                              Schema.Storage.FileSystem.DirectoryExists(resolvedPath).Passed;
            
            
            return CheckIf<object>(
                directoryExists || allowEmptyPath && string.IsNullOrEmpty(resolvedPath), 
                result: formattedPath,
                errorMessage: $"Directory '{resolvedPath}' does not exist",
                successMessage: directoryExists ? $"Directory '{resolvedPath}' exists" : "Empty path allowed", 
                context: context);
        }
    }
}