using System;
using System.IO;

namespace Schema.Core.IO
{
    public class LocalFileSystem : IFileSystem
    {
        #region File Operations
        public string ReadAllText(string filePath)
        {
            return File.ReadAllText(filePath);
        }

        public string[] ReadAllLines(string filePath)
        {
            return File.ReadAllLines(filePath);
        }

        public void WriteAllText(string filePath, string fileContent)
        {
            File.WriteAllText(filePath, fileContent);
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }
        
        #endregion
        
        #region Directory Operations
        
        public bool DirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }
            
            return Directory.Exists(directoryPath);
        }

        public void CreateDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            // Directory already exists, move on
            if (DirectoryExists(directoryPath))
            {
                return;
            }
            
            Directory.CreateDirectory(directoryPath);
        }
        
        #endregion
    }
}