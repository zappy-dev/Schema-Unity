namespace Schema.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class AttributeDefinition
    {
        public string AttributeName { get; set; }
        public DataType DataType { get; set; }
        public object DefaultValue { get; set; }
    }
}