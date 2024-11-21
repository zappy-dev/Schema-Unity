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

        public override SchemaResult CheckIfValidData(object value)
        {
            if (value == null)
            {
                return CheckIf(SupportsEmptyReferences, 
                    errorMessage: "Empty references are not allowed.",
                    successMessage: "Empty references are allowed.");
            }
            
            if (!Schema.GetScheme(ReferenceSchemeName).Try(out var refSchema))
            {
                return Fail($"Could not load Reference Scheme named '{ReferenceSchemeName}'");
            }

            if (!refSchema.GetIdentifierAttribute().Try(out var identifier))
            {
                return Fail("Reference Scheme has no Attribute marked as Identifier.");
            }

            if (identifier.AttributeName != ReferenceAttributeName)
            {
                return Fail($"Reference Scheme identifier {identifier} attribute does not match {ReferenceAttributeName}");
            }

            bool identifierExist = refSchema.GetIdentifierValues().Any(v => v.Equals(value));

            return CheckIf(identifierExist, 
                errorMessage: $"Value '{value}' does not exist as an identifier in {this}",
                successMessage: $"Value '{value}' exists as an identifier in {this}");
        }

        public override SchemaResult<object> ConvertData(object value)
        {
            var data = value as string;

            var validate = CheckIfValidData(data);
            return SchemaResult<object>.CheckIf(
                conditional: validate.Passed,
                result: data,
                errorMessage: validate.Message,
                successMessage: validate.Message,
                context: this);
        }
    }
}