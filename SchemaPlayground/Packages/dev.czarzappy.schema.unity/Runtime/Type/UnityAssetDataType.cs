using Schema.Core.Data;
using UnityEditor;
using UnityEngine;

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
            Debug.Log("Initializing UnityAssetDataType");
        }
    }
}