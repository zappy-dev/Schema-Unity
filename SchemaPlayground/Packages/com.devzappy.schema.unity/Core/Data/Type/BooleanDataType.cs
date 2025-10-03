using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class BooleanDataType : DataType
    {
        public override string TypeName => "Boolean";
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

        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            return CheckIf(value is bool, 
                errorMessage: "Value is not a boolean.",
                successMessage: "Value is a boolean.", context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object fromData)
        {
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