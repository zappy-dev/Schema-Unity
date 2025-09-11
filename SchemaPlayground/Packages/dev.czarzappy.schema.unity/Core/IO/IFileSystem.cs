namespace Schema.Core.IO
{
    public interface IFileSystem
    {
        SchemaResult<string> ReadAllText(string filePath);
        SchemaResult<string[]> ReadAllLines(string filePath);
        SchemaResult WriteAllText(string filePath, string fileContent);
        SchemaResult FileExists(string filePath);
        SchemaResult DirectoryExists(string directoryPath);
        SchemaResult CreateDirectory(string directoryPath);
    }
}