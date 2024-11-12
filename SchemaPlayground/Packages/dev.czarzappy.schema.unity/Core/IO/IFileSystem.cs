namespace Schema.Core.IO
{
    public interface IFileSystem
    {
        string ReadAllText(string filePath);
        string[] ReadAllLines(string filePath);
        void WriteAllText(string filePath, string fileContent);
        bool FileExists(string filePath);
        bool DirectoryExists(string directoryPath);
        void CreateDirectory(string directoryPath);
    }
}