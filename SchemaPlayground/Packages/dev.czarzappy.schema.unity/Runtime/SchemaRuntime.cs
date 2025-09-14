using System;
using System.IO;
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
        #region Constants
        public static string DEFAULT_PUBLISH_PATH = Path.Combine("Assets", "Plugins", "Schema");
        public static string DEFAULT_RESOURCE_PUBLISH_PATH = Path.Combine(DEFAULT_PUBLISH_PATH, "Resources");
        public static string DEFAULT_SCRIPTS_PUBLISH_PATH = Path.Combine("Assets", "Scripts", "Schemes");
        
        #endregion

        private static SchemaContext Context = new SchemaContext
        {
            Driver = "Runtime",
        };
        
        public static SchemaResult Initialize()
        {
            // TODO: This sets the core storage interface, overriding the editor interface
            Core.Schema.SetStorage(new Storage(new TextAssetResourcesFileSystem()));

            var ctx = new SchemaContext
            {
                Driver = "Runtime_Initialization"
            };
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
            Debug.Log(debug);
            try
            {
                var manifestAsset = Resources.Load<TextAsset>(Manifest.MANIFEST_SCHEME_NAME);

                var manifestDeserializeRes = Core.Schema.Storage.DefaultSchemaPublishFormat.Deserialize(context, manifestAsset.text);

                if (manifestDeserializeRes.Failed)
                {
                    return SchemaResult.Fail(context, manifestDeserializeRes.Message);
                }

                var manifestScheme = manifestDeserializeRes.Result;

                var manifestLoadRes = Core.Schema.LoadManifest(Context, manifestScheme);
                if (manifestLoadRes.Failed)
                {
                    return  SchemaResult.Fail(context, manifestLoadRes.Message);
                }
            }
            catch (Exception e)
            {
                return SchemaResult.Fail(context,$"Failed to load manifest file from Resources: {e.Message}");
            }

            return SchemaResult.Pass("Schema Runtime loaded!", context);
        }
    }
}