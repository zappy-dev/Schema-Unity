using System;
using System.Linq;

namespace Schema.Core.Data
{
    [Serializable]
    public class ReferenceDataType : DataType
    {
        protected override string Context => nameof(ReferenceDataType);
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
            
            // Set an initial default value
            if (Schema.GetScheme(ReferenceSchemeName).Try(out var refScheme))
            {
                var values = refScheme.GetIdentifierValues().Select(v => v?.ToString() ?? "").ToList();
                DefaultValue = values.Count > 0 ? values[0] : "";
            }
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
                return Fail("Could not load Reference Scheme");
            }

            if (!refSchema.GetIdentifierAttribute().Try(out var identifier))
            {
                return Fail("Reference Scheme does not contain Identifier Attribute.");
            }

            if (identifier.AttributeName != ReferenceAttributeName)
            {
                return Fail("Reference Scheme Identifier Attribute does not match");
            }

            bool identifierExist = refSchema.GetIdentifierValues().Any(v => v.Equals(value));

            return CheckIf(identifierExist, 
                errorMessage: "Value does not exist as an identifier",
                successMessage: "Value exists as an identifier");
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