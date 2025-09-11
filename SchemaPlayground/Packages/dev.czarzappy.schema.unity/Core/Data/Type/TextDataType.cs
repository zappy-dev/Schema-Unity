using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class TextDataType : DataType
    {
        public override SchemaContext Context => new SchemaContext
        {
            DataType = nameof(TextDataType),
        };
        
        public override string TypeName => "String"; // TODO: Rename to Text for general interpretability

        public TextDataType() : base(string.Empty)
        {
            
        }

        public TextDataType(string defaultValue) : base(defaultValue)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            return CheckIf(value is string, errorMessage: "Value is not text",
                successMessage: "Value is text", context: context);
        }

        public override SchemaResult<object> ConvertData(object value, SchemaContext context)
        {
            // TODO, handle formating for DateTimes explicitly
            var convertedData = value == null ? "" : value.ToString();
            return Pass<object>(result: convertedData,
                successMessage: "Converted string", context);
        }
    }
}