using System;

namespace Schema.Core
{
    [Serializable]
    public class ReferenceDataType : DataType
    {
        public string ReferenceSchemaName { get; set; }
        public string ReferenceAttributeName { get; set; }

        public ReferenceDataType()
        {
            
        }

        public ReferenceDataType(string schemaName, string identifierAttribute)
        {
            this.TypeName = $"Reference/{schemaName} - {identifierAttribute}";
            this.ReferenceSchemaName = schemaName;
            this.ReferenceAttributeName = identifierAttribute;
        }

        public override string ToString()
        {
            return $"ReferenceDataType: {ReferenceSchemaName}, Attribute: {ReferenceAttributeName}";
        }
    }
}