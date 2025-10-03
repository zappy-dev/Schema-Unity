using System;

namespace Schema.Core.Data
{
    public class GuidDataType : DataType
    {
        public override string TypeName => "Guid";
        public override object Clone()
        {
            return new GuidDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            return CheckIf(value is Guid, "Value is not a Guid", "Value is a Guid", context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object value)
        {
            if (value == null)
            {
                return SchemaResult<object>.Pass(CloneDefaultValue());
            }

            string strValue = value.ToString();
            bool parsed = System.Guid.TryParse(strValue, out var guid);

            return CheckIf<object>(parsed, guid, "Could not parse value as Guid", "Value parsed as Guid", context);
        }
    }
}