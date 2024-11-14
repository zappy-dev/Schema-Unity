namespace Schema.Core.Data
{
    public class TextDataType : DataType
    {
        public override string TypeName => "String"; // TODO: Rename to Text for general interpretability

        public TextDataType() : base(string.Empty)
        {
            
        }

        public TextDataType(string text) : base(text)
        {
            
        }
        
        public override bool IsValid(object value)
        {
            return value is string;
        }
    }
}