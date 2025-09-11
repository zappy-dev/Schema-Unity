using System;
using System.IO;

namespace Schema.Core.IO
{
    public class LocalFileSystem : IFileSystem
    {
        #region File Operations
        public SchemaResult<string> ReadAllText(string filePath)
        {
            return SchemaResult<string>.Pass(File.ReadAllText(filePath));
        }

        public SchemaResult<string[]> ReadAllLines(string filePath)
        {
            return SchemaResult<string[]>.Pass(File.ReadAllLines(filePath));
        }

        public SchemaResult WriteAllText(string filePath, string fileContent)
        {
            File.WriteAllText(filePath, fileContent);
            return SchemaResult.Pass();
        }

        public SchemaResult FileExists(string filePath)
        {
            return SchemaResult.CheckIf(File.Exists(filePath), "File does not exist");
        }
        
        #endregion
        
        #region Directory Operations
        
        public SchemaResult DirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }
            
            return SchemaResult.CheckIf(Directory.Exists(directoryPath),  "Directory does not exist");
        }

        public SchemaResult CreateDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            // Directory already exists, move on
            if (DirectoryExists(directoryPath).Passed)
            {
                return SchemaResult.Fail("Directory already exists");
            }
            
            Directory.CreateDirectory(directoryPath);
            
            return SchemaResult.Pass();
        }
        
        #endregion
    }
}