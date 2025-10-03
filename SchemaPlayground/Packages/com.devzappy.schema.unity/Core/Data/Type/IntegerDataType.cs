using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class IntegerDataType : DataType
    {
        public override string TypeName => "Integer";
        public override object Clone()
        {
            return new IntegerDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public IntegerDataType(int defaultValue = 0) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            return CheckIf(value is int, 
                errorMessage: "Value is not an integer.",
                successMessage: "Value is an integer.", context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object fromData)
        {
            try
            {
                var intData = Convert.ToInt32(fromData);
                return Pass<object>(intData,
                    successMessage: $"Value {fromData} is an integer.", context: context);
            }
            catch (FormatException e)
            {
                return Fail<object>($"Failed to convert from {fromData} to {TypeName}, error: {e.Message}", context: context);
            }
        }
    }
}