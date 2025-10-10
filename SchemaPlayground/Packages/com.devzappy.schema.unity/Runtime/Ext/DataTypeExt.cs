using System;
using Schema.Core;
using Schema.Core.CodeGen;
using Schema.Core.Data;
using Schema.Core.Ext;
using UnityEngine;
using Object = UnityEngine.Object;

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
    }
}
