namespace Schema.Core.Serialization
{
    public interface IStorageFormat<T>
    {
        /// File extension for the format
        string Extension { get; }
        SchemaResult<T> DeserializeFromFile(string filePath);
        SchemaResult<T> Deserialize(string content);
        SchemaResult SerializeToFile(string filePath, T data);
        SchemaResult<string> Serialize(T data);
    }
}