using System;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.IO;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    [Serializable]
    public class FilePathDataType : FSDataType
    {
        public override string TypeName => "FilePath";
        public override object Clone()
        {
            return new FilePathDataType
            {
                allowEmptyPath = allowEmptyPath,
                basePath = basePath,
                DefaultValue = DefaultValue,
                useRelativePaths = useRelativePaths
            };
        }

        public FilePathDataType(bool allowEmptyPath = true, bool useRelativePaths = false, string basePath = null) 
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

            Task<SchemaResult> fileExistsTask = storage.FileSystem.FileExists(context, resolvedPath);
            fileExistsTask.Wait(TimeSpan.FromSeconds(5)); // TODO: figure out timeouts in this context..
            
            return fileExistsTask.Result;
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            return ConvertValue(context, value, CancellationToken.None);
        }

        public SchemaResult<object> ConvertValue(SchemaContext context, object value, CancellationToken token = default)
        {
            using var _ = new DataTypeContextScope(ref context, this);
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

            Task<SchemaResult> doesFileExistTask = storage.FileSystem.FileExists(context, resolvedPath, token);
            doesFileExistTask.Wait(TimeSpan.FromSeconds(5)); // TODO: figure out timeouts in this context..
            
            bool fileExists = !string.IsNullOrWhiteSpace(resolvedPath) && 
                              doesFileExistTask.Result.Passed;
            
            return CheckIf<object>(
                fileExists || allowEmptyPath && string.IsNullOrEmpty(resolvedPath), 
                result: formattedPath,
                errorMessage: $"File '{resolvedPath}' does not exist",
                successMessage: fileExists ? $"File '{resolvedPath}' exists" : "Empty path allowed", 
                context: context);
        }
    }
}