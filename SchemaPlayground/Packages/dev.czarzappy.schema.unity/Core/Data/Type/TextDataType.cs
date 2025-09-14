using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class TextDataType : DataType
    {
        public override string TypeName => "String"; // TODO: Rename to Text for general interpretability
        public override object Clone()
        {
            return new TextDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public TextDataType() : base(string.Empty)
        {
            
        }

        public TextDataType(string defaultValue) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            return CheckIf(value is string, errorMessage: $"Value {value} is not text",
                successMessage: "Value is text", context: context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object value)
        {
            // TODO, handle formating for DateTimes explicitly
            var convertedData = value == null ? "" : value.ToString();
            return Pass<object>(result: convertedData,
                successMessage: "Converted string", context);
        }
    }
}