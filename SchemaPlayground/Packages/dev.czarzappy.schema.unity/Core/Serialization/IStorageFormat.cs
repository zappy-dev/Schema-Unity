namespace Schema.Core.Serialization
{
    public interface IStorageFormat<T>
    {
        /// File extension for the format
        string Extension { get; }
        bool TryDeserializeFromFile(string filePath, out T data);
        void SerializeToFile(string filePath, T data);
        string Serialize(T data);
    }
}