using System;
using System.IO;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Runtime.IO;
using UnityEngine;
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
            // Call this method to initialize Schema
            Schema.Core.Schema.ManifestUpdated += () =>
            {
                if (Application.isPlaying)
                {
                    onProjectLoad?.Invoke();
                }
            };
            
            // TODO: This sets the core storage interface, overriding the editor interface
            Core.Schema.SetStorage(new Storage(new TextAssetResourcesFileSystem()));

            var ctx = SchemaContextFactory.CreateRuntimeContext("Runtime_Initialization");
            return LoadFromResources(ctx);
        }
        
        /// <summary>
        /// Loads the runtime Schema data from Unity's Resources
        /// </summary>
        /// <returns></returns>
        private static SchemaResult LoadFromResources(SchemaContext context)
        {
            var textAssets = Resources.FindObjectsOfTypeAll<TextAsset>();

            var debug = string.Join(",", textAssets.Select(a => a.name));
            Logger.Log(debug);
            try
            {
                var manifestAsset = Resources.Load<TextAsset>(Manifest.MANIFEST_SCHEME_NAME);

                if (!Core.Schema.GetStorage(context).Try(out var storage, out var storageErr))
                {
                    return storageErr.Cast();
                }

                var manifestDeserializeRes = storage.DefaultSchemaPublishFormat.Deserialize(context, manifestAsset.text);

                if (manifestDeserializeRes.Failed)
                {
                    return SchemaResult.Fail(context, manifestDeserializeRes.Message);
                }

                var manifestScheme = manifestDeserializeRes.Result;

                var manifestLoadRes = Core.Schema.LoadManifest(context, manifestScheme, manifestImportPath: Manifest.MANIFEST_SCHEME_NAME, projectPath: string.Empty);
                if (manifestLoadRes.Failed)
                {
                    return  SchemaResult.Fail(context, manifestLoadRes.Message);
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