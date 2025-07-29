using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class TextDataType : DataType
    {
        protected override string Context => nameof(TextDataType);
        public override string TypeName => "String"; // TODO: Rename to Text for general interpretability

        public TextDataType() : base(string.Empty)
        {
            
        }

        public TextDataType(string text) : base(text)
        {
            
        }
        
        public override SchemaResult CheckIfValidData(object value)
        {
            return CheckIf(value is string, errorMessage: "Value is not text",
                successMessage: "Value is text");
        }

        public override SchemaResult<object> ConvertData(object value)
        {
            // TODO, handle formating for DateTimes explicitly
            var convertedData = value == null ? "" : value.ToString();
            return SchemaResult<object>.Pass(result: convertedData,
                successMessage: "Converted string", this);
        }
    }
}