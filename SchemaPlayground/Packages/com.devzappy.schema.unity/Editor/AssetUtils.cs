using System;
using UnityEditor;

namespace Schema.Unity.Editor
{
    public static class AssetUtils
    {
        public static bool TryLoadAssetFromGUID(Guid assetGuid, out UnityEngine.Object asset)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid.ToString().Replace("-", string.Empty));
            asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            return asset != null;
        }
    }
}