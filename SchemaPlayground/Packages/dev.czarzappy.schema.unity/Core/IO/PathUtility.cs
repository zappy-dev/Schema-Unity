using System;
using System.IO;

namespace Schema.Core.IO
{
    /// <summary>
    /// Utility class for handling file path operations, particularly for converting between absolute and relative paths.
    /// </summary>
    public static class PathUtility
    {
        /// <summary>
        /// Sanitizes the given path by converting platform-specific directory separators to the current platform
        /// </summary>
        /// <param name="path">Path to sanitize</param>
        /// <returns>Platform sanitized path</returns>
        public static string SanitizePath(string path)
        {
            switch (Path.DirectorySeparatorChar)
            {
                case '/':
                    path = path.Replace('\\', Path.DirectorySeparatorChar);
                    break;
                case '\\':
                    path = path.Replace('/', Path.DirectorySeparatorChar);
                    break;
            }
            
            return path;
        }
        
        /// <summary>
        /// Converts an absolute path to a path relative to the specified base directory.
        /// </summary>
        /// <param name="absolutePath">The absolute path to convert.</param>
        /// <param name="basePath">The base directory to make the path relative to.</param>
        /// <returns>A path relative to the base directory, or the original path if conversion is not possible.</returns>
        public static string MakeRelativePath(string absolutePath, string basePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return absolutePath;
                
            if (string.IsNullOrEmpty(basePath))
                return absolutePath;
                
            try
            {
                // Ensure paths are absolute and normalized
                absolutePath = Path.GetFullPath(absolutePath);
                basePath = Path.GetFullPath(basePath);
                
                // If the paths are on different drives, we can't make a relative path
                if (!Path.GetPathRoot(absolutePath).Equals(Path.GetPathRoot(basePath), StringComparison.OrdinalIgnoreCase))
                    return absolutePath;
                
                // Make sure basePath ends with directory separator
                if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    basePath += Path.DirectorySeparatorChar;
                
                // Get the URI for both paths
                Uri baseUri = new Uri(basePath);
                Uri absoluteUri = new Uri(absolutePath);
                
                // Get the relative path
                Uri relativeUri = baseUri.MakeRelativeUri(absoluteUri);
                
                // Convert to local path format and replace forward slashes with backslashes on Windows
                string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
                relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                
                return relativePath;
            }
            catch (Exception)
            {
                // If any error occurs, return the original path
                return absolutePath;
            }
        }
        
        /// <summary>
        /// Converts a relative path to an absolute path based on the specified base directory.
        /// </summary>
        /// <param name="relativePath">The relative path to convert.</param>
        /// <param name="basePath">The base directory to resolve the relative path from.</param>
        /// <returns>An absolute path, or the original path if it's already absolute.</returns>
        public static string MakeAbsolutePath(string relativePath, string basePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return relativePath;
                
            if (string.IsNullOrEmpty(basePath))
                return relativePath;
                
            try
            {
                // If the path is already absolute, return it
                if (Path.IsPathRooted(relativePath))
                    return relativePath;
                
                // Combine the base path with the relative path
                string absolutePath = Path.Combine(basePath, relativePath);
                
                // Normalize the path
                return Path.GetFullPath(absolutePath);
            }
            catch (Exception)
            {
                // If any error occurs, return the original path
                return relativePath;
            }
        }
        
        /// <summary>
        /// Determines if a path is absolute.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the path is absolute, false otherwise.</returns>
        public static bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            return Path.IsPathRooted(path);
        }
    }
}