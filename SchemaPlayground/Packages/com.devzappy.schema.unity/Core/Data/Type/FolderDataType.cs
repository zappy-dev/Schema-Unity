using Newtonsoft.Json;
using Schema.Core.IO;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    public class FolderDataType : FSDataType
    {
        public override string TypeName => "Folder";
        
        public override object Clone()
        {
            return new FolderDataType
            {
                allowEmptyPath = allowEmptyPath,
                basePath = basePath,
                DefaultValue = DefaultValue,
                useRelativePaths = useRelativePaths
            };
        }

        public FolderDataType(bool allowEmptyPath = true, bool useRelativePaths = false, string basePath = null) 
            : base(allowEmptyPath, useRelativePaths, basePath)
        {
        }
        
        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
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
            string resolvedPath = ResolvePath(context, filePath);
            
            if (!Schema.GetStorage(context).Try(out var storage, out var storageErr))
            {
                return storageErr.Cast();
            }

            bool directoryExists = storage.FileSystem.DirectoryExists(context, resolvedPath);
            return CheckIf(directoryExists, $"Directory '{resolvedPath}' do not exist", "Directory exists", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            if (!(value is string filePath))
            {
                return Fail<object>($"Value '{value}' is not a file path", context: context);
            }
            
            // Format the path according to our settings (relative/absolute)
            string formattedPath = FormatPath(context, filePath);
            Logger.LogDbgVerbose($"Formatted path: {formattedPath}");
            
            // For validation, we need to resolve to absolute path
            string resolvedPath = ResolvePath(context, filePath);
            Logger.LogDbgVerbose($"Resolved path: {resolvedPath}");
            
            if (!Schema.GetStorage(context).Try(out var storage, out var storageErr))
            {
                return storageErr.CastError<object>();
            }
            
            bool directoryExists = storage.FileSystem.DirectoryExists(context, resolvedPath);
            
            return CheckIf<object>(
                directoryExists || allowEmptyPath && string.IsNullOrEmpty(resolvedPath), 
                result: formattedPath,
                errorMessage: $"Directory '{resolvedPath}' does not exist",
                successMessage: directoryExists ? $"Directory '{resolvedPath}' exists" : "Empty path allowed", 
                context: context);
        }
    }
}