namespace Schema.Core.Serialization
{
    public interface IStorageFormat<T>
    {
        /// File extension for the format
        string Extension { get; }
        bool TryDeserializeFromFile(string filePath, out T data);
        bool TryDeserialize(string content, out T data);
        SchemaResult SerializeToFile(string filePath, T data);
        string Serialize(T data);
    }
}