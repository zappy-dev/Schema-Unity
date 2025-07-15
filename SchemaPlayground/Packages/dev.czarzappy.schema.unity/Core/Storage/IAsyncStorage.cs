using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Schema.Core.Storage
{
    /// <summary>
    /// Interface for asynchronous storage operations
    /// </summary>
    public interface IAsyncStorage
    {
        /// <summary>
        /// Checks if a file exists asynchronously
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if file exists, false otherwise</returns>
        Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deserializes data from a file asynchronously
        /// </summary>
        /// <typeparam name="TResult">Type to deserialize to</typeparam>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized data</returns>
        Task<TResult> DeserializeFromFileAsync<TResult>(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Serializes data to a file asynchronously
        /// </summary>
        /// <typeparam name="TData">Type of data to serialize</typeparam>
        /// <param name="path">Path to the file</param>
        /// <param name="data">Data to serialize</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SerializeToFileAsync<TData>(string path, TData data, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes a file asynchronously
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Reads all text from a file asynchronously
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as string</returns>
        Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Writes text to a file asynchronously
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="content">Content to write</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets file information asynchronously
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File information</returns>
        Task<FileInfo> GetFileInfoAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Creates a directory asynchronously
        /// </summary>
        /// <param name="path">Path to the directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if a directory exists asynchronously
        /// </summary>
        /// <param name="path">Path to the directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if directory exists, false otherwise</returns>
        Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Copies a file asynchronously
        /// </summary>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="overwrite">Whether to overwrite existing file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Moves a file asynchronously
        /// </summary>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Result of a storage operation
    /// </summary>
    public class StorageResult
    {
        public bool Success { get; }
        public string Message { get; }
        public Exception Exception { get; }
        
        private StorageResult(bool success, string message, Exception exception)
        {
            Success = success;
            Message = message ?? string.Empty;
            Exception = exception;
        }
        
        public static StorageResult Successful(string message = null)
        {
            return new StorageResult(true, message, null);
        }
        
        public static StorageResult Failed(string message, Exception exception = null)
        {
            return new StorageResult(false, message, exception);
        }
    }
    
    /// <summary>
    /// Result of a storage operation with a value
    /// </summary>
    public class StorageResult<T>
    {
        public bool Success { get; }
        public T Value { get; }
        public string Message { get; }
        public Exception Exception { get; }
        
        private StorageResult(bool success, T value, string message, Exception exception)
        {
            Success = success;
            Value = value;
            Message = message ?? string.Empty;
            Exception = exception;
        }
        
        public static StorageResult<T> Successful(T value, string message = null)
        {
            return new StorageResult<T>(true, value, message, null);
        }
        
        public static StorageResult<T> Failed(string message, Exception exception = null)
        {
            return new StorageResult<T>(false, default(T), message, exception);
        }
        
        public bool TryGetValue(out T value)
        {
            value = Value;
            return Success;
        }
    }
}