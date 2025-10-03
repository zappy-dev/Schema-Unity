using Schema.Core.Data;
using UnityEditor;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Runtime.Type
{
    public class UnityAssetDataType : GuidDataType
    {
        public override string TypeName => $"Unity Asset/{ObjectType.Name}";
        public readonly System.Type ObjectType;

        public UnityAssetDataType(System.Type objectType)
        {
            if (objectType == null)
            {
                objectType = typeof(Object);
            }
            ObjectType = objectType;
        }

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            AddPluginType(new UnityAssetDataType(typeof(Texture)));
            Logger.LogVerbose("Initializing UnityAssetDataType");
        }
    }
}