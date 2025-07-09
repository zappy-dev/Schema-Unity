using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class BooleanDataType : DataType
    {
        public override string TypeName => "Boolean";

        public BooleanDataType(bool defaultValue = false) : base(defaultValue)
        {
        }

        public BooleanDataType() : base(false)
        {
        }

        public override SchemaResult CheckIfValidData(object value)
        {
            return CheckIf(value is bool, 
                errorMessage: $"Value '{value}' is not a boolean.",
                successMessage: $"Value '{value}' is a boolean.");
        }

        public override SchemaResult<object> ConvertData(object fromData)
        {
            try
            {
                if (fromData is bool b)
                    return SchemaResult<object>.Pass(b, successMessage: $"Value {fromData} is a boolean.", context: this);
                if (fromData is string s && bool.TryParse(s, out var parsed))
                    return SchemaResult<object>.Pass(parsed, successMessage: $"Parsed '{s}' as boolean.", context: this);
                if (fromData is int i)
                    return SchemaResult<object>.Pass(i != 0, successMessage: $"Converted int {i} to boolean.", context: this);
                return SchemaResult<object>.Fail($"Failed to convert from {fromData} to Boolean", context: this);
            }
            catch (Exception e)
            {
                return SchemaResult<object>.Fail($"Failed to convert from {fromData} to Boolean, error: {e.Message}", context: this);
            }
        }
    }
} 