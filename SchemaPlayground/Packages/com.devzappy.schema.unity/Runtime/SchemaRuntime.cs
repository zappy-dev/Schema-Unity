using System;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using UnityEngine;
using static Schema.Core.Schema;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Runtime
{
    /// <summary>
    /// Represents the runtime interface for loading and retrieving game configs
    /// </summary>
    public static class SchemaRuntime
    {
        public static SchemaResult Initialize(Action onProjectLoad = null)
        {
            Logger.Level = Logger.LogLevel.VERBOSE;
            Logger.Log("SchemaRuntime Initialize");
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            _ = InitializeAsync(onProjectLoad, cts.Token);
            return SchemaResult.Pass("Initializing..");
            // // Call this method to initialize Schema
            // ManifestUpdated += () =>
            // {
            //     if (Application.isPlaying)
            //     {
            //         onProjectLoad?.Invoke();
            //     }
            // };
            //
            // // TODO: This sets the core storage interface, overriding the editor interface
            // // Core.Schema.SetStorage(new Storage(new TextAssetResourcesFileSystem()));
            // SetStorage(new Storage(new RemoteFileSystem("http://localhost:4566/schema-bucket")));
            //
            // var ctx = SchemaContextFactory.CreateRuntimeContext("Runtime_Initialization");
            // var cts = new CancellationTokenSource();
            // cts.CancelAfter(TimeSpan.FromSeconds(10));
            // var loadTask = LoadFromResources(ctx, cts.Token);
            // loadTask.Wait(TimeSpan.FromSeconds(10)); // TODO: Figure out a better API for async loading
            // return loadTask.Result;
        }

        public static async Task<SchemaResult> InitializeAsync(Action onProjectLoad, CancellationToken cancellationToken)
        {
            Logger.Log("Initializing Schema Runtime");
            // Call this method to initialize Schema
            ManifestUpdated += () =>
            {
                if (Application.isPlaying)
                {
                    onProjectLoad?.Invoke();
                }
            };
            
            // TODO: This sets the core storage interface, overriding the editor interface
            // Core.Schema.SetStorage(new Storage(new TextAssetResourcesFileSystem()));
            SetStorage(new Storage(new RemoteFileSystem($"http://localhost:4566/schema-bucket/{DEFAULT_RESOURCE_PUBLISH_PATH}")));

            var ctx = SchemaContextFactory.CreateRuntimeContext("Runtime_Initialization");
            return await LoadFromResources(ctx, cancellationToken);
        }
        
        /// <summary>
        /// Loads the runtime Schema data from Unity's Resources
        /// </summary>
        /// <returns></returns>
        private static async Task<SchemaResult> LoadFromResources(SchemaContext context, CancellationToken cancellationToken = default)
        {
            Logger.Log("Loading Resources");
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // var manifestAsset = Resources.Load<TextAsset>(Manifest.MANIFEST_SCHEME_NAME);

                if (!GetStorage(context).Try(out var storage, out var storageErr))
                {
                    return storageErr.Cast();
                }

                var publishFormat = storage.DefaultSchemaPublishFormat;
                string manifestFileName = publishFormat.ResolveFileName(Manifest.MANIFEST_SCHEME_NAME);
                // string basePath = DEFAULT_RESOURCE_PUBLISH_PATH; // TODO: Make this better..
                string manifestPublishPath = $"{DefaultContentDirectory}/{manifestFileName}";
                
                var manifestDeserializeRes = await storage.DefaultSchemaPublishFormat.DeserializeFromFile(context, manifestPublishPath, cancellationToken);

                // var manifestDeserializeRes = storage.DefaultSchemaPublishFormat.Deserialize(context, manifestAsset.text);

                if (manifestDeserializeRes.Failed)
                {
                    return SchemaResult.Fail(context, manifestDeserializeRes.Message);
                }

                var manifestScheme = manifestDeserializeRes.Result;

                var runtimeLoadConfig = new SchemeLoadConfig();
                runtimeLoadConfig.overwriteExisting = true;
                runtimeLoadConfig.runValidation = false;

                var manifestLoadRes = await LoadManifest(context, manifestScheme, 
                    manifestImportPath: manifestFileName, 
                    projectPath: string.Empty,
                    loadConfig: runtimeLoadConfig,
                    cancellationToken: cancellationToken);
                if (manifestLoadRes.Failed)
                {
                    return SchemaResult.Fail(context, manifestLoadRes.Message);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                return SchemaResult.Fail(context,$"Failed to load manifest file from Resources: {e.Message}");
            }

            return SchemaResult.Pass("Schema Runtime loaded!", context);
        }
    }
}