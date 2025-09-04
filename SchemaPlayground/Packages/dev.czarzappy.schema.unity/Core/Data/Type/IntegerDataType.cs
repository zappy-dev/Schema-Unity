using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class IntegerDataType : DataType
    {
        public override SchemaContext Context => new SchemaContext()
        {
            DataType = nameof(IntegerDataType),
        };
        
        public override string TypeName => "Integer";

        public IntegerDataType(int defaultValue = 0) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            return CheckIf(value is int, 
                errorMessage: "Value is not an integer.",
                successMessage: "Value is an integer.", context);
        }

        public override SchemaResult<object> ConvertData(object fromData, SchemaContext context)
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