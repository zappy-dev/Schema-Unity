using Schema.Core;
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

        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute)
        {
            return SchemaResult<string>.Fail($"Method {nameof(GetDataMethod)} not implemented on {nameof(UnityAssetDataType)}", context);
        }
        
        public override string CSDataType => ObjectType.ToString();

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
            Logger.LogVerbose("Initializing Unity Asset DataTypes");
            AddPluginType(new UnityAssetDataType(typeof(GameObject)));
            AddPluginType(new UnityAssetDataType(typeof(Texture)));
        }
    }
}