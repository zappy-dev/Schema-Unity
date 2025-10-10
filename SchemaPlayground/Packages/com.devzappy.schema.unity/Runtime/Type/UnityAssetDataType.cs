using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Schemes;
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
            // Target API
            // public UnityEngine.Texture Sprite => DataEntry.GetDataAsObject<UnityEngine.Texture>("Sprite");
            return SchemaResult<string>.Pass($"{nameof(EntryWrapper.DataEntry)}.GetDataAsObject<{ObjectType}>(\"{attribute.AttributeName}\").Result");
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
            AddPluginType(new UnityAssetDataType(typeof(Sprite)));
        }
    }
}