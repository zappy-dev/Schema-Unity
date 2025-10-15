using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class BooleanDataType : DataType
    {
        public override string TypeName => "Boolean";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => SchemaResult<string>.Pass($"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsBool)}(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(bool).ToString();

        public override object Clone()
        {
            return new BooleanDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public BooleanDataType(bool defaultValue = false) : base(defaultValue)
        {
        }

        public BooleanDataType() : base(false)
        {
        }

        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            return CheckIf(value is bool,
#if SCHEMA_DEBUG
                errorMessage: $"Value '{value}' is not a boolean.",
#else
                errorMessage: "Value is not a boolean.",
#endif
                successMessage: "Value is a boolean.", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object fromData)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            try
            {
                if (fromData is bool b)
                    return Pass<object>(b, successMessage: "Value is a boolean.", context: context);
                if (fromData is string s && bool.TryParse(s, out var parsed))
                    return Pass<object>(parsed, successMessage: "Parsed as boolean.", context: context);
                if (fromData is int i)
                    return Pass<object>(i != 0, successMessage: "Converted int to boolean.", context: context);
                return Fail<object>($"Failed to convert {fromData} to Boolean", context: context);
            }
            catch (Exception e)
            {
                return Fail<object>($"Failed to convert to Boolean, error: {e.Message}", context: context);
            }
        }
    }
} 