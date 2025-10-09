using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class FloatingPointDataType : DataType
    {
        public override string TypeName => "Float";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => SchemaResult<string>.Pass($"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsFloat)}(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(float).ToString();
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
        
        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            return CheckIf(value is float || value is double, 
                errorMessage: "Value is not an floating point number.",
                successMessage: "Value is an floating point number.", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object fromData)
        {
            try
            {
                var intData = Convert.ToDouble(fromData);
                return Pass<object>(intData,
                    successMessage: $"Value {fromData} is an floating point number.", context: context);
            }
            catch (Exception e)
            {
                return Fail<object>($"Failed to convert from {fromData} to {TypeName}, error: {e.Message}", context: context);
            }
        }
    }
}