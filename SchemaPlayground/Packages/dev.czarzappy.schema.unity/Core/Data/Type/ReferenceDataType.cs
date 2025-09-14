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

        public override object Clone()
        {
            return new ReferenceDataType
            {
                DefaultValue = DefaultValue,
                ReferenceSchemeName = ReferenceSchemeName,
                ReferenceAttributeName = ReferenceAttributeName,
                SupportsEmptyReferences = SupportsEmptyReferences
            };
        }

        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            if (value == null)
            {
                return CheckIf(SupportsEmptyReferences, 
                    errorMessage: "Empty references are not allowed.",
                    successMessage: "Empty references are allowed.", context);
            }
            
            // what if the referenced scheme is the self scheme?
            if (!Schema.GetScheme(ReferenceSchemeName).Try(out var refSchema))
            {
                if (context.Scheme.SchemeName == ReferenceSchemeName)
                {
                    refSchema = context.Scheme;
                }
                else
                {
                    return Fail("Could not load Reference Scheme", context);
                }
            }

            if (!refSchema.GetIdentifierAttribute().Try(out var identifier))
            {
                return Fail("Reference Scheme does not contain Identifier Attribute.", context);
            }

            if (identifier.AttributeName != ReferenceAttributeName)
            {
                return Fail("Reference Scheme Identifier Attribute does not match", context);
            }

            // Problem
            // we're in the middle of loading the data schemes...
            // we're assuming that all of the identifier values have been converted to their expected types
            // Weirder situation, the value is the correct type, and the identifier hasn't been mapped yet :(
            // Amount of time debugging this problem: 3 hours

            // HACK: Convert all of the id values here
            // Probably expensive to do this all of the time, assumes the values are not valid
            // Converting for every new entry value added
            var finalizedIdValues = refSchema.GetIdentifierValues()
                .Select(idValue =>
                {
                    var isValidRes = identifier.DataType.CheckIfValidData(context, idValue);
                    if (isValidRes.Passed) return idValue;

                    var convertRes = identifier.DataType.ConvertData(context, idValue);
                    return convertRes.Result;
                });
            var matchingIdentifier = finalizedIdValues.FirstOrDefault(v => v.Equals(value));
            if (matchingIdentifier == null)
            {
                return Fail("Value does not exist as an identifier", context);
            }

            // HACK: Do the conversion for the identifier value here...

            var isValidIdRes = identifier.DataType.CheckIfValidData(context, matchingIdentifier);
            if (isValidIdRes.Failed)
            {
                var convertRes = identifier.DataType.ConvertData(context, matchingIdentifier);
                if (convertRes.Failed)
                {
                    // Weird if the referenced identifier cannot convert to the expected type here
                    return Fail(convertRes.Message, context);
                }

                // finally set the correct value for the matching identifier
                matchingIdentifier = convertRes.Result;
            }

            // confirm again that the source value matches the identifier value
            var matchesIdentifier = matchingIdentifier.Equals(value);
            // if (matchingIdentifier == null)
            // {
            //     return Fail("Value does not exist as an identifier", context);
            // }
            //
            // if (matchingIdentifier.GetType() != value.GetType())
            // {
            //     return Fail("Value type does not match identifier type", context);
            // }
            //
            // return Pass("Value matches a referenced identifier", context);
            return CheckIf(matchesIdentifier, 
                errorMessage: "Value does not match identifier, likely type mismatch",
                successMessage: "Value exists as an identifier", context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object value)
        {
            // this is incorrect
            // var data = value as string;
            
            // First, convert the value to the same data type as a reference's attribute
            if (!Schema.GetScheme(ReferenceSchemeName).Try(out var refSchema))
            {
                if (context.Scheme != null && 
                    context.Scheme.SchemeName == ReferenceSchemeName)
                {
                    refSchema = context.Scheme;
                }
                else
                {
                    return Fail<object>("Could not load Reference Scheme", context);
                }
            }

            if (!refSchema.GetIdentifierAttribute().Try(out var identifier))
            {
                return Fail<object>("Reference Scheme does not contain Identifier Attribute.", context);
            }

            var isValidRes = identifier.DataType.CheckIfValidData(context, value);

            if (isValidRes.Failed)
            {
                var convertRes = identifier.DataType.ConvertData(context, value);

                if (convertRes.Failed)
                {
                    return Fail<object>(convertRes.Message, context);
                }

                // set value to the converted data type for the referenced attribute
                value = convertRes.Result;
            }
            
            // Then check if that matches an existing identifier
            var validate = CheckIfValidData(context, value);
            
            // If it doesn't, the conversion failed
            return SchemaResult<object>.CheckIf(
                conditional: validate.Passed,
                result: value,
                errorMessage: validate.Message,
                successMessage: validate.Message,
                context: this);
        }
    }
}