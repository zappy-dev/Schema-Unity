using System;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Runtime.IO;
using UnityEngine;

namespace Schema.Runtime
{
    /// <summary>
    /// Represents the runtime interface for loading and retrieving game configs
    /// </summary>
    public static class SchemaRuntime
    {
        #region Constatns

        public static string DEFAULT_PUBLISH_PATH = "Assets/Plugins/Schema";
        public static string DEFAULT_RESOURCE_PUBLISH_PATH = $"{DEFAULT_PUBLISH_PATH}/Resources";
        
        #endregion

        private static SchemaContext Context = new SchemaContext
        {
            Driver = "Runtime",
        };
        
        public static SchemaResult Initialize()
        {
            // TODO: This sets the core storage interface, overriding the editor interface
            Core.Schema.SetStorage(new Storage(new TextAssetResourcesFileSystem()));
            return LoadFromResources();
        }
        
        /// <summary>
        /// Loads the runtime Schema data from Unity's Resources
        /// </summary>
        /// <returns></returns>
        private static SchemaResult LoadFromResources()
        {
            var textAssets = Resources.FindObjectsOfTypeAll<TextAsset>();

            var debug = string.Join(",", textAssets.Select(a => a.name));
            Debug.Log(debug);
            try
            {
                var manifestAsset = Resources.Load<TextAsset>(Manifest.MANIFEST_SCHEME_NAME);

                var manifestDeserializeRes = Core.Schema.Storage.DefaultSchemaPublishFormat.Deserialize(manifestAsset.text);

                if (manifestDeserializeRes.Failed)
                {
                    return SchemaResult.Fail(manifestDeserializeRes.Message, manifestDeserializeRes.Context);
                }

                var manifestScheme = manifestDeserializeRes.Result;

                var manifestLoadRes = Core.Schema.LoadManifest(manifestScheme, Context);
                if (manifestLoadRes.Failed)
                {
                    return  SchemaResult.Fail(manifestLoadRes.Message, manifestLoadRes.Context);
                }
            }
            catch (Exception e)
            {
                return SchemaResult.Fail($"Failed to load manifest file from Resources: {e.Message}");
            }

            return SchemaResult.Pass("Schema Runtime loaded!");
        }
    }
}