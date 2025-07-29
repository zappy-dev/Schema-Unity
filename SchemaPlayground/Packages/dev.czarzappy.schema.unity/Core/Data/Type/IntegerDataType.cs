using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class IntegerDataType : DataType
    {
        protected override string Context => nameof(IntegerDataType);
        public override string TypeName => "Integer";

        public IntegerDataType(int defaultValue = 0) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(object value)
        {
            return CheckIf(value is int, 
                errorMessage: "Value is not an integer.",
                successMessage: "Value is an integer.");
        }

        public override SchemaResult<object> ConvertData(object fromData)
        {
            try
            {
                var intData = Convert.ToInt32(fromData);
                return SchemaResult<object>.Pass(intData,
                    successMessage: $"Value {fromData} is an integer.", context: this);
            }
            catch (FormatException e)
            {
                return SchemaResult<object>.Fail($"Failed to convert from {fromData} to {TypeName}, error: {e.Message}", context: this);
            }
        }
    }
}