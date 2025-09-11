using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class FloatingPointDataType : DataType
    {
        public override SchemaContext Context => new SchemaContext()
        {
            DataType = nameof(FloatingPointDataType),
        };
        
        public override string TypeName => "Float";

        public FloatingPointDataType(float defaultValue = 0f) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            return CheckIf(value is float || value is double, 
                errorMessage: "Value is not an floating point number.",
                successMessage: "Value is an floating point number.", context);
        }

        public override SchemaResult<object> ConvertData(object fromData, SchemaContext context)
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