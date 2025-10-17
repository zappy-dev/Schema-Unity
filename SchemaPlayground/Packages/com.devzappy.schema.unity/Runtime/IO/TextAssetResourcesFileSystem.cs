using System;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core;
using Schema.Core.Ext;
using Schema.Core.IO;
using UnityEngine;

namespace Schema.Runtime.IO
{
    public class TextAssetResourcesFileSystem : IFileSystem
    {
        public async Task<SchemaResult<string>> ReadAllText(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            if (!(await LoadTextAsset(context, filePath, cancellationToken)).Try(out var textAsset, out var error))
            {
                return error.CastError<string>();
            }

            return SchemaResult<string>.Pass(textAsset.text);
        }

        public async Task<SchemaResult<string[]>> ReadAllLines(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            if (!(await LoadTextAsset(context, filePath, cancellationToken)).Try(out var textAsset, out var error))
            {
                return error.CastError<string[]>();
            }

            var rows = textAsset.text.SplitByLineEndings();
            return SchemaResult<string[]>.Pass(rows);
        }

        public Task<SchemaResult> WriteAllText(SchemaContext context, string filePath, string fileContent, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Cannot write text file in Resources folder.");
        }

        public async Task<SchemaResult> FileExists(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            return (await LoadTextAsset(context, filePath, cancellationToken)).Cast();
        }

        private Task<SchemaResult<TextAsset>> LoadTextAsset(SchemaContext context, string filePath, CancellationToken cancellationToken = default)
        {
            var res = SchemaResult<TextAsset>.New(context);
            if (!ResourcesUtils.SanitizeResourcePath(context, filePath).Try(out var sanitizedPath, out var error))
            {
                return Task.FromResult(error.CastError(res));
            }

            try
            {
                var tcs = new TaskCompletionSource<SchemaResult<TextAsset>>(TaskCreationOptions.RunContinuationsAsynchronously);

                var request = Resources.LoadAsync<TextAsset>(sanitizedPath);
                
                void Complete(AsyncOperation op)
                {
                    // Unity invokes AsyncOperation.completed on main thread
                    request.completed -= Complete;

                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    // If the path/type mismatch, asset may be null
                    if (request.asset is TextAsset asset)
                        tcs.TrySetResult(res.Pass(asset));
                    else
                        tcs.TrySetResult(res.Fail($"{sanitizedPath} does not exist in Resources folder."));
                        // tcs.TrySetException(new InvalidOperationException(
                        //     $"Resources.LoadAsync<{typeof(T).Name}>(\"{path}\") returned null (wrong path or type)."));
                }
                if (cancellationToken.CanBeCanceled)
                {
                    // If user cancels before completion, stop tracking and cancel the Task
                    cancellationToken.Register(() =>
                    {
                        request.completed -= Complete;
                        tcs.TrySetCanceled(cancellationToken);
                    });
                }

                request.completed += Complete;

                return tcs.Task;
                // var textAsset = ;
                // return res.CheckIf(textAsset == true, textAsset, $"{sanitizedPath} does not exist in Resources folder.");
            }
            catch (Exception ex)
            {
                return Task.FromResult(res.Fail($"{sanitizedPath} does not exist in Resources folder, reason: {ex.Message}"));
            }
        }

        public Task<bool> DirectoryExists(SchemaContext context, string directoryPath, CancellationToken token = default)
        {
            throw new InvalidOperationException("Directory does not exist in Resources folder.");
        }

        public Task<SchemaResult> CreateDirectory(SchemaContext context, string directoryPath, CancellationToken token = default)
        {
            throw new InvalidOperationException("Unable to create directory in Resources folder.");
        }
    }
}