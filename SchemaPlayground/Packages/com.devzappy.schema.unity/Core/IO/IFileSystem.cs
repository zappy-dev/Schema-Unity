using System.Threading;
using System.Threading.Tasks;

namespace Schema.Core.IO
{
    public interface IFileSystem
    {
        Task<SchemaResult<string>> ReadAllText(SchemaContext context, string filePath, CancellationToken cancellationToken = default);
        Task<SchemaResult<string[]>> ReadAllLines(SchemaContext context, string filePath, CancellationToken cancellationToken = default);
        Task<SchemaResult> WriteAllText(SchemaContext context, string filePath, string fileContent, CancellationToken cancellationToken = default);
        Task<SchemaResult> FileExists(SchemaContext context, string filePath, CancellationToken cancellationToken = default);
        Task<bool> DirectoryExists(SchemaContext context, string directoryPath, CancellationToken cancellationToken = default);
        Task<SchemaResult> CreateDirectory(SchemaContext context, string directoryPath, CancellationToken cancellationToken = default);
    }
}