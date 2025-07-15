using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Serialization;
using System.Collections.Generic;

namespace Schema.Core.Storage
{
    /// <summary>
    /// Async file storage implementation that wraps current synchronous operations
    /// </summary>
    public class AsyncFileStorage : IAsyncStorage
    {
        private readonly IStorageFormat _storageFormat;
        private readonly IFileSystem _fileSystem;
        
        public AsyncFileStorage(IStorageFormat storageFormat = null, IFileSystem fileSystem = null)
        {
            _storageFormat = storageFormat ?? Storage.DefaultManifestStorageFormat;
            _fileSystem = fileSystem ?? Storage.FileSystem;
        }
        
        public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return await Task.Run(() => _fileSystem.FileExists(path), cancellationToken);
        }
        
        public async Task<TResult> DeserializeFromFileAsync<TResult>(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return await Task.Run(() =>
            {
                var result = _storageFormat.DeserializeFromFile<TResult>(path);
                if (result.Try(out var value))
                {
                    return value;
                }
                throw new InvalidOperationException($"Failed to deserialize from file: {path}");
            }, cancellationToken);
        }
        
        public async Task SerializeToFileAsync<TData>(string path, TData data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Run(() =>
            {
                var result = _storageFormat.SerializeToFile(path, data);
                if (result.Failed)
                {
                    throw new InvalidOperationException($"Failed to serialize to file: {path} - {result.Message}");
                }
            }, cancellationToken);
        }
        
        public async Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Run(() =>
            {
                if (_fileSystem.FileExists(path))
                {
                    File.Delete(path);
                }
            }, cancellationToken);
        }
        
        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return await Task.Run(() => File.ReadAllText(path), cancellationToken);
        }
        
        public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Run(() =>
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(path, content);
            }, cancellationToken);
        }
        
        public async Task<FileInfo> GetFileInfoAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return await Task.Run(() => new FileInfo(path), cancellationToken);
        }
        
        public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Run(() =>
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }, cancellationToken);
        }
        
        public async Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            return await Task.Run(() => Directory.Exists(path), cancellationToken);
        }
        
        public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Run(() =>
            {
                // Ensure destination directory exists
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                
                File.Copy(sourcePath, destinationPath, overwrite);
            }, cancellationToken);
        }
        
        public async Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await Task.Run(() =>
            {
                // Ensure destination directory exists
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                
                File.Move(sourcePath, destinationPath);
            }, cancellationToken);
        }
    }
    
    /// <summary>
    /// Async storage implementation for testing and mocking
    /// </summary>
    public class MockAsyncStorage : IAsyncStorage
    {
        private readonly Dictionary<string, object> _storage = new Dictionary<string, object>();
        private readonly Dictionary<string, string> _textStorage = new Dictionary<string, string>();
        
        public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            return _storage.ContainsKey(path) || _textStorage.ContainsKey(path);
        }
        
        public async Task<TResult> DeserializeFromFileAsync<TResult>(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            
            if (_storage.TryGetValue(path, out var value) && value is TResult result)
            {
                return result;
            }
            
            throw new FileNotFoundException($"File not found: {path}");
        }
        
        public async Task SerializeToFileAsync<TData>(string path, TData data, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            _storage[path] = data;
        }
        
        public async Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            _storage.Remove(path);
            _textStorage.Remove(path);
        }
        
        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            
            if (_textStorage.TryGetValue(path, out var content))
            {
                return content;
            }
            
            throw new FileNotFoundException($"File not found: {path}");
        }
        
        public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            _textStorage[path] = content;
        }
        
        public async Task<FileInfo> GetFileInfoAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            
            if (!_storage.ContainsKey(path) && !_textStorage.ContainsKey(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            
            return new FileInfo(path);
        }
        
        public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            // No-op for mock implementation
        }
        
        public async Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            return true; // Always exists for mock
        }
        
        public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken); // Simulate async behavior
            
            if (_storage.TryGetValue(sourcePath, out var value))
            {
                if (overwrite || !_storage.ContainsKey(destinationPath))
                {
                    _storage[destinationPath] = value;
                }
            }
            else if (_textStorage.TryGetValue(sourcePath, out var textValue))
            {
                if (overwrite || !_textStorage.ContainsKey(destinationPath))
                {
                    _textStorage[destinationPath] = textValue;
                }
            }
            else
            {
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
            }
        }
        
        public async Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            await CopyFileAsync(sourcePath, destinationPath, true, cancellationToken);
            await DeleteFileAsync(sourcePath, cancellationToken);
        }
    }
}