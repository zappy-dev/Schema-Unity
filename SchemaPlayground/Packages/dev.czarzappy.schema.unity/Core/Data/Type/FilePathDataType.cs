using System;
using System.IO;
using Newtonsoft.Json;
using Schema.Core.IO;

namespace Schema.Core.Data
{
    [Serializable]
    public class FilePathDataType : DataType
    {
        protected override string Context => nameof(FilePathDataType);
        [JsonProperty("AllowEmptyPath")]
        private bool allowEmptyPath;
        
        [JsonProperty("UseRelativePaths")]
        private bool useRelativePaths;
        
        [JsonProperty("BasePath")]
        private string basePath;
        
        [JsonIgnore]
        public bool AllowEmptyPath => allowEmptyPath;
        
        [JsonIgnore]
        public bool UseRelativePaths => useRelativePaths;
        
        [JsonIgnore]
        public string BasePath => basePath;
        
        public override string TypeName => "FilePath";

        public FilePathDataType(bool allowEmptyPath = true, bool useRelativePaths = false, string basePath = null) : base(string.Empty)
        {
            this.allowEmptyPath = allowEmptyPath;
            this.useRelativePaths = useRelativePaths;
            this.basePath = basePath;
        }
        
        /// <summary>
        /// Gets the content base path, either from the provided base path or from the default content path
        /// </summary>
        protected virtual string GetContentBasePath()
        {
            if (!string.IsNullOrEmpty(basePath))
                return basePath;
                
            // Try to get the default content path from Schema
            if (!Schema.IsInitialized)
            {
                return null;
            }
            
            var manifestPath = Schema.ManifestImportPath;
            if (!string.IsNullOrEmpty(manifestPath))
            {
                return Path.GetDirectoryName(manifestPath);
            }
            
            return null;
        }
        
        /// <summary>
        /// Converts a path to the appropriate format (relative or absolute) based on the data type settings
        /// </summary>
        protected virtual string FormatPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
                
            if (!useRelativePaths)
                return path;
                
            var contentBasePath = GetContentBasePath();
            if (string.IsNullOrEmpty(contentBasePath))
                return path;
                
            // If the path is absolute, convert it to relative
            if (PathUtility.IsAbsolutePath(path))
            {
                return PathUtility.MakeRelativePath(path, contentBasePath);
            }
            
            return path;
        }
        
        /// <summary>
        /// Resolves a path to its absolute form for file system operations
        /// </summary>
        protected string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
                
            if (PathUtility.IsAbsolutePath(path))
                return path;
                
            var contentBasePath = GetContentBasePath();
            if (string.IsNullOrEmpty(contentBasePath))
                return path;
                
            return PathUtility.MakeAbsolutePath(path, contentBasePath);
        }
        
        public override SchemaResult CheckIfValidData(object value)
        {
            if (!(value is string filePath))
            {
                return Fail("Value is not a file path");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return CheckIf(allowEmptyPath, errorMessage: "File path is empty", successMessage: "File path is set");
            }

            if (PathUtility.IsAbsolutePath(filePath) && useRelativePaths)
            {
                return Fail("File path is absolute when only relative paths are supported");
            }
            
            // Resolve the path to absolute for file system check
            string resolvedPath = ResolvePath(filePath);
            
            return CheckIf(Serialization.Storage.FileSystem.FileExists(resolvedPath), 
                errorMessage: "File does not exist",
                successMessage: "File exists");
        }

        public override SchemaResult<object> ConvertData(object value)
        {
            if (!(value is string filePath))
            {
                return SchemaResult<object>.Fail($"Value '{value}' is not a file path", context: this);
            }
            
            // Format the path according to our settings (relative/absolute)
            string formattedPath = FormatPath(filePath);
            
            // For validation, we need to resolve to absolute path
            string resolvedPath = ResolvePath(filePath);
            
            bool fileExists = !string.IsNullOrWhiteSpace(resolvedPath) && 
                              Serialization.Storage.FileSystem.FileExists(resolvedPath);
            
            return SchemaResult<object>.CheckIf(
                fileExists || allowEmptyPath && string.IsNullOrEmpty(resolvedPath), 
                result: formattedPath,
                errorMessage: $"File '{resolvedPath}' does not exist",
                successMessage: fileExists ? $"File '{resolvedPath}' exists" : "Empty path allowed", 
                context: this);
        }
    }
}