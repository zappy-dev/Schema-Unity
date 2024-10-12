namespace Schema.Core
{
    public interface IStorageFormat
    {
        /// File extension for the format
        string Extension { get; }  
        T Load<T>(string filePath);
        void Save<T>(string filePath, T data);
    }
}