using System;

namespace Schema.Core.Data
{
    public class GuidDataType : DataType
    {
        public override string TypeName => "Guid";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => SchemaResult<string>.Pass($"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsGuid)}(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(Guid).ToString();
        public override object Clone()
        {
            return new GuidDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public GuidDataType() : base(System.Guid.Empty)
        {
            
        }

        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            bool isGuid = value is Guid;
            return CheckIf(isGuid, $"Value '{value}' is not a Guid", "Value is a Guid", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
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