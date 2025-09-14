using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class FloatingPointDataType : DataType
    {
        public override string TypeName => "Float";
        public override object Clone()
        {
            return new FloatingPointDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public FloatingPointDataType(float defaultValue = 0f) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            return CheckIf(value is float || value is double, 
                errorMessage: "Value is not an floating point number.",
                successMessage: "Value is an floating point number.", context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object fromData)
        {
            try
            {
                var intData = Convert.ToDouble(fromData);
                return Pass<object>(intData,
                    successMessage: $"Value {fromData} is an floating point number.", context: context);
            }
            catch (FormatException e)
            {
                return Fail<object>($"Failed to convert from {fromData} to {TypeName}, error: {e.Message}", context: context);
            }
        }
    }
}