namespace Schema.Core
{
    public interface IStorageFormat<T>
    {
        /// File extension for the format
        string Extension { get; }  
        T Load(string filePath);
        void Save(string filePath, T data);
    }
}