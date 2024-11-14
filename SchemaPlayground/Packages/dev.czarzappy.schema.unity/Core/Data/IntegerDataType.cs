namespace Schema.Core.Data
{
    public class IntegerDataType : DataType
    {
        public override string TypeName => "Integer";
        public override bool IsValid(object value)
        {
            return value is int;
        }

        public IntegerDataType(int defaultValue = 0) : base(defaultValue)
        {
            
        }
    }
}