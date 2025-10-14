using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class IntegerDataType : DataType
    {
        public override string TypeName => "Integer";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => SchemaResult<string>.Pass($"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsInt)}(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(int).ToString();
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
        
        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            return CheckIf(value is int,
#if SCHEMA_DEBUG
                errorMessage: $"Value '{value}' is not an integer.",
#else
                errorMessage: $"Value is not an integer.",
#endif
                successMessage: "Value is an integer.", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object fromData)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            try
            {
                var intData = Convert.ToInt32(fromData);
                return Pass<object>(intData,
                    successMessage: $"Value '{fromData}' is an integer.", context: context);
            }
            catch (Exception formatEx)
            {
                return Fail<object>($"Failed to convert from '{fromData}' to {TypeName}, error: {formatEx.Message}", context: context);
            }
        }
    }
}