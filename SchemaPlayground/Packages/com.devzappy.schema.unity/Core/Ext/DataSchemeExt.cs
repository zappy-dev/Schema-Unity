using Schema.Core.Data;

namespace Schema.Core
{
    public static class DataSchemeExt
    {
        /// <remark>This extension is here to support an API update to DataScheme.SetDataOnEntry between v0.2 to v0.3.
        /// This API update caused v0.2 code-gen'd files to get out-of-sync with Schema v0.3's API
        /// </remark>
        public static SchemaResult SetDataOnEntry(this DataScheme scheme, DataEntry entry, string attributeName, object value,
            bool allowIdentifierUpdate = false, bool shouldDirtyScheme = true)
        {
            return scheme.SetDataOnEntry(CodeGen.CodeGenUtils.Context, entry, attributeName, value, allowIdentifierUpdate,
                shouldDirtyScheme);
        }
    }
}