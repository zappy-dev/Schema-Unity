using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class BooleanDataType : DataType
    {
        public override SchemaContext Context => new SchemaContext()
        {
            DataType = nameof(BooleanDataType),
        };
        
        public override string TypeName => "Boolean";

        public BooleanDataType(bool defaultValue = false) : base(defaultValue)
        {
        }

        public BooleanDataType() : base(false)
        {
        }

        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            return CheckIf(value is bool, 
                errorMessage: "Value is not a boolean.",
                successMessage: "Value is a boolean.", context);
        }

        public override SchemaResult<object> ConvertData(object fromData, SchemaContext context)
        {
            try
            {
                if (fromData is bool b)
                    return Pass<object>(b, successMessage: "Value is a boolean.", context: context);
                if (fromData is string s && bool.TryParse(s, out var parsed))
                    return Pass<object>(parsed, successMessage: "Parsed as boolean.", context: context);
                if (fromData is int i)
                    return Pass<object>(i != 0, successMessage: "Converted int to boolean.", context: context);
                return Fail<object>("Failed to convert to Boolean", context: context);
            }
            catch (Exception e)
            {
                return Fail<object>($"Failed to convert to Boolean, error: {e.Message}", context: context);
            }
        }
    }
} 