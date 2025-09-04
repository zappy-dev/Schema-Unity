using System;

namespace Schema.Core.Data
{
    public class GuidDataType : DataType
    {
        public override SchemaContext Context => new SchemaContext()
        {
            DataType = nameof(GuidDataType)
        };
        
        public override string TypeName => "Guid";
        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            return CheckIf(value is Guid, "Value is not a Guid", "Value is a Guid", context);
        }

        public override SchemaResult<object> ConvertData(object value, SchemaContext context)
        {
            if (value == null)
            {
                return Fail<object>("Cannot convert null value to Guid", context);
            }

            string strValue = value.ToString();
            bool parsed = System.Guid.TryParse(strValue, out var guid);

            return CheckIf<object>(parsed, guid, "Could not parse value as Guid", "Value parsed as Guid", context);
        }
    }
}