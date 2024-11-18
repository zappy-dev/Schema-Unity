using Schema.Core.Data;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Ext
{
    internal static class ObjectExt
    {
        internal static SchemaResult IsValidForDataType(this object obj, DataType dataType)
        {
            if (obj == null)
            {
                return Fail("Object cannot be null");
            }

            return dataType.CheckIfValidData(obj);
        }
    }
}