using System;

namespace Schema.Core.Ext
{
    public static class GuidExt
    {
        public static string ToAssetGuid(this Guid guid)
        {
            return guid.ToString().Replace("-", string.Empty);
        }
    }
}