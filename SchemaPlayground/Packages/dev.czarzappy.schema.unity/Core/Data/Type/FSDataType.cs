using Newtonsoft.Json;
using Schema.Core.IO;

namespace Schema.Core.Data
{
    public abstract class FSDataType : DataType
    {
        #region Fields and Properties
        
        [JsonProperty("AllowEmptyPath")]
        protected bool allowEmptyPath;
        
        [JsonProperty("UseRelativePaths")]
        protected bool useRelativePaths;
        
        [JsonProperty("BasePath")]
        protected string basePath;
        
        [JsonIgnore]
        public bool AllowEmptyPath => allowEmptyPath;
        
        [JsonIgnore]
        public bool UseRelativePaths => useRelativePaths;
        
        [JsonIgnore]
        public string BasePath => basePath;

        #endregion
        
        public FSDataType(bool allowEmptyPath = true, bool useRelativePaths = false, string basePath = null) : base(string.Empty)
        {
            this.allowEmptyPath = allowEmptyPath;
            this.useRelativePaths = useRelativePaths;
            this.basePath = basePath;
        }

        /// <summary>
        /// Gets the base path, either from the provided base path or from the default project path
        /// </summary>
        protected string ResolveBasePath()
        {
            if (!string.IsNullOrEmpty(basePath))
                return basePath;
                
            // Try to get the default content path from Schema
            if (!Schema.IsInitialized)
            {
                return null;
            }

            var manifestPath = Schema.ProjectPath;
            if (!string.IsNullOrEmpty(manifestPath))
            {
                return manifestPath;
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
                
            var contentBasePath = ResolveBasePath();
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
                
            var basePath = ResolveBasePath();
            if (string.IsNullOrEmpty(basePath))
                return path;
                
            return PathUtility.MakeAbsolutePath(path, basePath);
        }
    }
}