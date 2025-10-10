using System;
using Schema.Core.Ext;
using UnityEditor;

namespace Schema.Unity.Editor
{
    public static class AssetUtils
    {
        public static bool TryLoadAssetFromGUID(Guid assetGuid, Type assetType, out UnityEngine.Object asset)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid.ToAssetGuid());
            
            switch (assetPath)
            {
                case "Resources/unity_builtin_extra":
                    // TODO: Support Built In Assets
                    // Is this going to exist in future Unity versions?
                    asset = null;
                    return false;
                default:
                    asset = AssetDatabase.LoadAssetAtPath(assetPath, assetType);
                    break;
            }

            return asset != null;
        }
    }
}