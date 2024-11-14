using System;
using System.Linq;

namespace Schema.Core.Data
{
    [Serializable]
    public class ReferenceDataType : DataType
    {
        public const string TypeNamePrefix = "Reference";
        
        public string ReferenceSchemeName { get; set; }
        public string ReferenceAttributeName { get; set; }

        public bool SupportsEmptyReferences { get; set; } = true;

        public ReferenceDataType()
        {
            DefaultValue = string.Empty;
        }

        public ReferenceDataType(string schemeName, string identifierAttribute) : base(null)
        {
            ReferenceSchemeName = schemeName;
            ReferenceAttributeName = identifierAttribute;
        }

        public override string TypeName => $"{TypeNamePrefix}/{ReferenceSchemeName} - {ReferenceAttributeName}";

        public override string ToString()
        {
            return $"ReferenceDataType: {ReferenceSchemeName}, Attribute: {ReferenceAttributeName}";
        }

        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return SupportsEmptyReferences;
            }
            
            if (!Schema.TryGetScheme(ReferenceSchemeName, out var refSchema))
            {
                Logger.LogWarning(
                    $"Could not load Reference Scheme named '{ReferenceSchemeName}'");
                return false;
            }

            if (!refSchema.TryGetIdentifierAttribute(out var identifier))
            {
                Logger.LogWarning("Reference Scheme has no Attribute marked as Identifier.");
                return false;
            }

            if (identifier.AttributeName != ReferenceAttributeName)
            {
                Logger.LogWarning(
                    $"Reference Scheme identifier {identifier} attribute does not match {ReferenceAttributeName}");
                return false;
            }

            bool identifierExist = refSchema.GetIdentifierValues().Any(v => v.Equals(value));
            if (!identifierExist)
            {
                Logger.LogWarning($"Value '{value}' does not exist as an identifier in {this}");
            }

            return identifierExist;
        }
    }
}