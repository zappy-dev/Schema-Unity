using System;
using Schema.Core;
using Schema.Core.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Schema.Runtime
{
    public static class DataTypeExt
    {
        public static Object GetDataAsObject(this DataEntry dataEntry, string attribute)
        {
            if (!dataEntry.TryGetDataAsString(attribute, out string assetGuid))
            {
                return null;
            }

            var ctx = new SchemaContext
            {
                Driver = "DataEntry_Conversion",
            };

            if (Schema.Core.Schema.GetOwnerSchemeForAttribute(ctx, attribute).Try(out var res))
            {
                
            }

            throw new NotImplementedException();

            return Resources.Load(assetGuid);
        }
    }
}
