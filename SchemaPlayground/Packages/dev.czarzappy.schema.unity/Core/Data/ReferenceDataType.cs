using System;

namespace Schema.Core
{
    [Serializable]
    public class ReferenceDataType : DataType
    {
        public string ReferenceSchemeName { get; set; }
        public string ReferenceAttributeName { get; set; }

        public ReferenceDataType()
        {
            
        }

        public ReferenceDataType(string schemeName, string identifierAttribute)
        {
            TypeName = $"Reference/{schemeName} - {identifierAttribute}";
            ReferenceSchemeName = schemeName;
            ReferenceAttributeName = identifierAttribute;
        }

        public override string ToString()
        {
            return $"ReferenceDataType: {ReferenceSchemeName}, Attribute: {ReferenceAttributeName}";
        }
    }
}