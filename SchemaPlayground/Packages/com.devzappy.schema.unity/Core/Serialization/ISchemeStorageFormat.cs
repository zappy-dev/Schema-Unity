using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    /// <summary>
    /// Defines a storage format capable of serializing and deserializing <see cref="DataScheme"/>
    /// to and from in-memory text and files on disk.
    /// </summary>
    public interface ISchemeStorageFormat
    {
        /// <summary>
        /// File extension associated with this format.
        ///
        /// Examples:
        /// - g.cs
        /// - csv
        /// - json
        /// - asset
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Human-readable name for the format (e.g., "JSON").
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Indicates whether this format supports importing (deserialization).
        /// </summary>
        bool IsImportSupported { get; }

        /// <summary>
        /// Indicates whether this format supports exporting (serialization).
        /// </summary>
        bool IsExportSupported { get; }

        /// <summary>
        /// Deserializes a <see cref="DataScheme"/> from a file on disk.
        /// </summary>
        /// <param name="context">Serialization context used to resolve types and options.</param>
        /// <param name="filePath">Path to the input file to read.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A <see cref="SchemaResult{T}"/> containing the <see cref="DataScheme"/> or errors.</returns>
        Task<SchemaResult<DataScheme>> DeserializeFromFile(SchemaContext context, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deserializes a <see cref="DataScheme"/> from a string payload.
        /// </summary>
        /// <param name="context">Serialization context used to resolve types and options.</param>
        /// <param name="content">The textual content to parse.</param>
        /// <returns>A <see cref="SchemaResult{T}"/> containing the <see cref="DataScheme"/> or errors.</returns>
        SchemaResult<DataScheme> Deserialize(SchemaContext context, string content);

        /// <summary>
        /// Serializes a <see cref="DataScheme"/> and writes it to a file on disk.
        /// </summary>
        /// <param name="context">Serialization context providing options and environment.</param>
        /// <param name="filePath">Destination file path to write.</param>
        /// <param name="data">The <see cref="DataScheme"/> to serialize.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A <see cref="SchemaResult"/> indicating success or failure.</returns>
        Task<SchemaResult> SerializeToFile(SchemaContext context, string filePath, DataScheme data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Serializes a <see cref="DataScheme"/> into a string payload.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="data">The <see cref="DataScheme"/> to serialize.</param>
        /// <returns>A <see cref="SchemaResult{T}"/> containing the serialized string or errors.</returns>
        SchemaResult<string> Serialize(SchemaContext context, DataScheme data);
    }
}