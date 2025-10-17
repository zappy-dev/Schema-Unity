using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Logging;
using static Schema.Core.SchemaResult;

namespace Schema.Core.IO
{
    /// <summary>
    /// IFileSystem implementation that downloads files via HTTP/HTTPS.
    /// This is a read-only file system suitable for downloading published content from a CDN or remote server.
    /// </summary>
    public class RemoteFileSystem : IFileSystem
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        /// <summary>
        /// Creates a new RemoteFileSystem instance.
        /// </summary>
        /// <param name="baseUrl">Base URL for remote files (e.g., "http://localhost:4566/schema-bucket/schema")</param>
        /// <param name="httpClient">Optional HttpClient instance (will create a new one if not provided)</param>
        public RemoteFileSystem(string baseUrl, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));
            }

            _baseUrl = SanitizePath(baseUrl);
            _httpClient = httpClient ?? new HttpClient();
            
            Logger.Log($"RemoteFileSystem initialized with base URL: {_baseUrl}");
        }

        #region File Operations

        /// <summary>
        /// Reads all text from a remote file via HTTP GET.
        /// </summary>
        /// <param name="context">Schema context</param>
        /// <param name="filePath">Relative file path (will be appended to base URL)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as string</returns>
        public async Task<SchemaResult<string>> ReadAllText(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var res = SchemaResult<string>.New(context);
            var url = BuildUrl(filePath);
            
            Logger.LogDbgVerbose($"Reading remote file: {url}");

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = $"Failed to read remote file '{filePath}'. HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    Logger.LogWarning(errorMsg);
                    return res.Fail(errorMsg);
                }

                var content = await response.Content.ReadAsStringAsync();
                Logger.Log($"Read remote file: {content.Substring(0, 100)}...");
                return res.Pass(content);
            }
            catch (HttpRequestException ex)
            {
                var errorMsg = $"HTTP error reading file '{filePath}': {ex.Message}";
                Logger.LogError(errorMsg);
                return res.Fail(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error reading file '{filePath}': {ex.Message}";
                Logger.LogError(errorMsg);
                return res.Fail(errorMsg);
            }
        }

        /// <summary>
        /// Reads all lines from a remote file via HTTP GET.
        /// </summary>
        /// <param name="context">Schema context</param>
        /// <param name="filePath">Relative file path (will be appended to base URL)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as array of lines</returns>
        public async Task<SchemaResult<string[]>> ReadAllLines(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            var res = SchemaResult<string[]>.New(context);
            var textResult = await ReadAllText(context, filePath, cancellationToken);
            
            if (textResult.Failed)
            {
                return textResult.CastError(res);
            }

            var lines = textResult.Result.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            return SchemaResult<string[]>.Pass(lines);
        }

        /// <summary>
        /// Checks if a remote file exists by sending an HTTP HEAD request.
        /// </summary>
        /// <param name="context">Schema context</param>
        /// <param name="filePath">Relative file path (will be appended to base URL)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success if file exists, failure otherwise</returns>
        public async Task<SchemaResult> FileExists(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(filePath);
            Logger.LogDbgVerbose($"Checking if remote file exists: {url}");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _httpClient.SendAsync(request, cancellationToken);

                return CheckIf(context, response.IsSuccessStatusCode, 
                    $"Remote file not found: {filePath}");
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"HTTP error checking file existence '{filePath}': {ex.Message}");
                return Fail(context, $"HTTP error checking file: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking file existence '{filePath}': {ex.Message}");
                return Fail(context, $"Error checking file: {ex.Message}");
            }
        }

        /// <summary>
        /// Write operations are not supported for remote HTTP file systems.
        /// </summary>
        public Task<SchemaResult> WriteAllText(SchemaContext context, string filePath, string fileContent, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Write operations are not supported for RemoteFileSystem. This is a read-only file system.");
        }

        #endregion

        #region Directory Operations

        /// <summary>
        /// Directory operations are not supported for remote HTTP file systems.
        /// </summary>
        public Task<bool> DirectoryExists(SchemaContext context, string directoryPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Directory operations are not supported for RemoteFileSystem.");
        }

        /// <summary>
        /// Directory operations are not supported for remote HTTP file systems.
        /// </summary>
        public Task<SchemaResult> CreateDirectory(SchemaContext context, string directoryPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Directory operations are not supported for RemoteFileSystem.");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Builds the full URL by combining the base URL with the file path.
        /// </summary>
        /// <param name="filePath">Relative file path</param>
        /// <returns>Complete URL</returns>
        private string BuildUrl(string filePath)
        {
            // Remove leading slash if present to avoid double slashes
            var cleanPath = SanitizePath(filePath);
            return $"{_baseUrl}/{cleanPath}";
        }

        public static string SanitizePath(string url)
        {
            var cleanPath = url.TrimStart('/');
            cleanPath = cleanPath.TrimEnd('/');
            return cleanPath.Replace('\\', '/');
        }

        #endregion
    }
}

