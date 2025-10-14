using System;
using Schema.Core;
using Schema.Core.CodeGen;
using Schema.Core.Data;
using Schema.Core.Ext;
using UnityEngine;
using Object = UnityEngine.Object;
using static Schema.Core.SchemaResult;

namespace Schema.Runtime
{
    public static class DataTypeExt
    {
        public static SchemaResult<T> GetDataAsObject<T>(this DataEntry dataEntry, string attribute) where T : Object
        {
            var res = SchemaResult<T>.New(CodeGenUtils.Context);
            var assetGuid = dataEntry.GetDataAsGuid(attribute);
            if (assetGuid == Guid.Empty)
            {
                return res.Fail("Asset guid not found");
            }
            
            if (!UnityAssetLinker.Instance.SearchForAsset(CodeGenUtils.Context, assetGuid.ToAssetGuid()).Try(out var asset, out var searchErr)) return searchErr.CastError<T>();

            
            if (!ResourcesUtils.SanitizeResourcePath(CodeGenUtils.Context, asset.Path).Try(out var sanitizedPath, out var error))
            {
                return error.CastError<T>();
            }
            
            var loadedAsset = Resources.Load<T>(sanitizedPath);
            return res.Pass(loadedAsset);
        }
        
        public static SchemaResult<Color> GetDataAsColor(this DataEntry dataEntry, string attribute)
        {
            var res = SchemaResult<Color>.New(CodeGenUtils.Context);
            var hexString = dataEntry.GetDataAsString(attribute);
            
            if (string.IsNullOrWhiteSpace(hexString))
            {
                return res.Pass(Color.black, "Empty color value, using black");
            }
            
            if (!ColorUtility.TryParseHtmlString(hexString, out Color color))
            {
                return res.Fail($"Failed to parse color from hex string '{hexString}'");
            }
            
            return res.Pass(color, $"Successfully parsed color from '{hexString}'");
        }
    }
}
