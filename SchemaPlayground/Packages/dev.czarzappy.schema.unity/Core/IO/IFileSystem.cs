namespace Schema.Core.IO
{
    public interface IFileSystem
    {
        SchemaResult<string> ReadAllText(SchemaContext context, string filePath);
        SchemaResult<string[]> ReadAllLines(SchemaContext context, string filePath);
        SchemaResult WriteAllText(SchemaContext context, string filePath, string fileContent);
        SchemaResult FileExists(SchemaContext context, string filePath);
        bool DirectoryExists(SchemaContext context, string directoryPath);
        SchemaResult CreateDirectory(SchemaContext context, string directoryPath);
    }
}