using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core;
using UnityEditor;
using UnityEngine;
using static Schema.Core.SchemaResult;
using Object = UnityEngine.Object;

namespace Schema.Runtime
{
    public class UnityAssetLinker : SingletonScriptableObject<UnityAssetLinker>
    {
        private const string SCHEMA_ASSET_LINKER_ASSET_NAME = "Schema Asset Linker";

#if UNITY_EDITOR
        
        internal static UnityAssetLinker CreateAsset()
        {
            var newInstance = ScriptableObject.CreateInstance<UnityAssetLinker>();
            AssetDatabase.CreateAsset(newInstance, $"Assets/Plugins/Schema/Resources/{nameof(UnityAssetLinker)}.asset");
            return newInstance;
        }
#endif
        
        [SerializeField]
        private List<AssetLink> assetLinks;

        public SchemaResult<AssetLink> SearchForAsset(SchemaContext ctx, string assetGuid)
        {
            var res = SchemaResult<AssetLink>.New(ctx);
            if (assetLinks == null || assetLinks.Count == 0) return res.Fail("No asset links found");
            
            var assetLink = assetLinks.FirstOrDefault((link) =>  link.Guid == assetGuid);
            
            return res.CheckIf(assetLink.IsSet, assetLink, $"No asset link found for {assetGuid}");
        }

        internal SchemaResult AddAssetLink(SchemaContext ctx, AssetLink assetLink)
        {
            if (assetLink.Asset is null)
            {
                return Fail(ctx, "Attempting to add asset link without asset");
            }
            
            if (assetLinks == null)
            {
                assetLinks = new List<AssetLink>();
            }
            
            if (assetLinks.Contains(assetLink))
            {
                return Pass();
            }
            
            assetLinks.Add(assetLink);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            
            return Pass();
        }
    }

    [Serializable]
    public struct AssetLink : IEquatable<AssetLink>
    {
        [SerializeField]
        private Object asset;
        
        [SerializeField]
        private string assetGUID;
        public string Guid => assetGUID;
        
        [SerializeField]
        private string assetPath;

        public AssetLink(string guid, string assetPath, Object asset)
        {
            this.asset = asset;
            this.assetGUID = guid;
            this.assetPath = assetPath;
        }
        
        public bool IsSet =>  !(asset is null &&  assetGUID == null && assetPath == null);
        public string Path => assetPath;
        public object Asset => asset;

        public bool Equals(AssetLink other)
        {
            if (asset is null ^ other.asset is null)
            {
                return false;
            }

            if (!(asset is null && other.asset is null))
            {
                if (!Equals(asset, other.asset))
                {
                    return false;
                }
            }
            
            return assetGUID == other.assetGUID && assetPath == other.assetPath;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetLink other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (!(asset is null) ? asset.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (assetGUID != null ? assetGUID.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (assetPath != null ? assetPath.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
