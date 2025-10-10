using System;
using System.IO;
using Schema.Core;
using Schema.Core.IO;

namespace Schema.Runtime
{
    internal static class ResourcesUtils
    {
        internal const string RESOURCES_FOLDER_NAME = "Resources";
        
        internal static SchemaResult<string> SanitizeResourcePath(SchemaContext context, string path)
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
    }
}