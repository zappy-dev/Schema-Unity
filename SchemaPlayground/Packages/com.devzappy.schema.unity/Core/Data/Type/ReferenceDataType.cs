using System;
using System.Linq;
using Schema.Core.Serialization;

namespace Schema.Core.Data
{
    [Serializable]
    public class ReferenceDataType : DataType
    {
        public const string TypeNamePrefix = "Reference";
        
        public string ReferenceSchemeName { get; set; }
        public string ReferenceAttributeName { get; set; }

        public bool SupportsEmptyReferences { get; set; } = true;
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute)
        {
            if (!GetReferencedIdentifierAttribute(context).Try(out var refAttribute, out var refError)) 
                return refError.CastError<string>();
            
            if (!refAttribute.DataType.GetDataMethod(context, attribute).Try(out var getDataMethod, out var getDataError)) 
                return getDataError.CastError<string>();
            
            // NPCScheme.GetEntry(DataEntry.GetDataAsString("NPC")).Result
            var refSchemeWrapperIdentifier = CSharpSchemeStorageFormat.SchemeClassIdentifier(ReferenceSchemeName);
            return SchemaResult<string>.Pass($"{refSchemeWrapperIdentifier}.GetEntry({getDataMethod}).Result");
            
            // return $"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsString)}(\"{attribute.AttributeName}\")";
        }

        public override string CSDataType => CSharpSchemeStorageFormat.SchemeEntryClassIdentifier(ReferenceSchemeName);

        public ReferenceDataType()
        {
            DefaultValue = string.Empty;
        }

        public ReferenceDataType(string schemeName, string identifierAttribute, bool validateSchemeLoaded = true) : base(null)
        {
            ReferenceSchemeName = schemeName;
            ReferenceAttributeName = identifierAttribute;

            var ctx = new SchemaContext
            {
                Driver = "Reference_DataType_Constructor",
                DataType = $"Reference/{ReferenceSchemeName} (ID: {ReferenceAttributeName})"
            };
            
            // Set an initial default value
            if (validateSchemeLoaded && Schema.GetScheme(ctx, ReferenceSchemeName).Try(out var refScheme))
            {
                var values = refScheme.GetIdentifierValues().Select(v => v?.ToString() ?? "").ToList();
                DefaultValue = values.Count > 0 ? values[0] : "";
            }
        }

        public override string TypeName => $"{TypeNamePrefix}/{ReferenceSchemeName} - {ReferenceAttributeName}";

        public override string ToString()
        {
            return $"ReferenceDataType[Scheme: '{ReferenceSchemeName}', Attribute: '{ReferenceAttributeName}']";
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

        public SchemaResult<AttributeDefinition> GetReferencedIdentifierAttribute(SchemaContext context)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            
            if (!GetReferencedScheme(context).Try(out var refSchema, out var refErr))
                return refErr.CastError<AttributeDefinition>();

            if (!refSchema.GetIdentifierAttribute().Try(out var identifier))
            {
                return Fail<AttributeDefinition>("Reference Scheme does not contain Identifier Attribute.", context);
            }

            if (identifier.AttributeName != ReferenceAttributeName)
            {
                return Fail<AttributeDefinition>("Reference Scheme Identifier Attribute does not match", context);
            }

            return refSchema.GetAttributeByName(identifier.AttributeName, context);
        }

        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            
            if (value == null)
            {
                return CheckIf(SupportsEmptyReferences, 
                    errorMessage: "Empty references are not allowed.",
                    successMessage: "Empty references are allowed.", context);
            }
            
            if (!GetReferencedScheme(context).Try(out var refSchema, out var refErr))
                return refErr.Cast();

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
                    var isValidRes = identifier.DataType.IsValidValue(context, idValue);
                    if (isValidRes.Passed) return idValue;

                    var convertRes = identifier.DataType.ConvertValue(context, idValue);
                    return convertRes.Result;
                });
            var matchingIdentifier = finalizedIdValues.FirstOrDefault(v => v.Equals(value));
            if (matchingIdentifier == null)
            {
                return Fail($"Value '{value}' does not exist as an identifier", context);
            }

            // HACK: Do the conversion for the identifier value here...

            var isValidIdRes = identifier.DataType.IsValidValue(context, matchingIdentifier);
            if (isValidIdRes.Failed)
            {
                var convertRes = identifier.DataType.ConvertValue(context, matchingIdentifier);
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

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            // this is incorrect
            // var data = value as string;
            
            // First, convert the value to the same data type as a reference's attribute
            if (!GetReferencedScheme(context).Try(out var refSchema, out var refError))
                return refError.CastError<object>();

            if (!refSchema.GetIdentifierAttribute().Try(out var identifier))
            {
                return Fail<object>("Reference Scheme does not contain Identifier Attribute.", context);
            }

            var isValidRes = identifier.DataType.IsValidValue(context, value);

            if (isValidRes.Failed)
            {
                var convertRes = identifier.DataType.ConvertValue(context, value);

                if (convertRes.Failed)
                {
                    return Fail<object>(convertRes.Message, context);
                }

                // set value to the converted data type for the referenced attribute
                value = convertRes.Result;
            }
            
            // Then check if that matches an existing identifier
            var validate = IsValidValue(context, value);
            
            // If it doesn't, the conversion failed
            return SchemaResult<object>.CheckIf(
                conditional: validate.Passed,
                result: value,
                errorMessage: validate.Message,
                successMessage: validate.Message,
                context: this);
        }

        private SchemaResult<DataScheme> GetReferencedScheme(SchemaContext context)
        {
            // Try normal API first (requires initialization)
            var refScheme = Schema.GetScheme(context, ReferenceSchemeName);
            if (refScheme.Passed)
            {
                return refScheme;
            }

            var res = SchemaResult<DataScheme>.New(context);
            // Fallback: if the current context is already for the referenced scheme
            if (context.Scheme != null && context.Scheme.SchemeName == ReferenceSchemeName)
            {
                return res.Pass(context.Scheme);
            }

            // Fallback: consult already loaded schemes without going through IsInitialized gate
            if (Schema.LoadedSchemes.TryGetValue(ReferenceSchemeName, out var loaded))
            {
                return res.Pass(loaded);
            }

            return res.Fail("Reference scheme does not exist");
        }
    }
}