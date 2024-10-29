namespace Schema.Core
{
    [System.Serializable]
    public class AttributeDefinition
    {
        public string AttributeName { get; set; }
        public DataType DataType { get; set; }
        public object DefaultValue { get; set; }
    }
}