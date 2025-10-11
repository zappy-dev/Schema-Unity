using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;
using static Schema.Core.SchemaResult;

namespace Schema.Core
{
    public static partial class Schema
    {
        /// <summary>
        /// Runs a series of validation passes against a scheme
        /// 1. Checks that there are no duplicate identifier values, if an identifier attribute exists.
        /// 2. Checks that all entry values are present
        /// 3. Checks that all entry values are valid for their associated attribute data type.
        /// </summary>
        /// <param name="ctx">Operation context</param>
        /// <param name="schemeToValidate">Scheme to validate</param>
        /// <returns></returns>
        public static SchemaResult IsValidScheme(SchemaContext ctx, DataScheme schemeToValidate)
        {
            // Validate that there are no duplicate identifiers.
            if (schemeToValidate.GetIdentifierAttribute().Try(out var idAttr))
            {
                using var _ = new AttributeContextScope(ref ctx, idAttr);
                var idValues = schemeToValidate.GetRawIdentifierValues().Select(id => id.ToString()).ToArray();
                var uniques = new HashSet<string>(idValues);

                if (idValues.Length > uniques.Count)
                {
                    return Fail(ctx, $"Schema contains multiple {idValues.Length - uniques.Count} duplicate identifiers");
                }
            }
            
            var schemeAttributes = schemeToValidate.GetAttributes();
            return BulkResult(schemeToValidate.AllEntries,
                errorMessage: "Failed to validate all entries",
                context: ctx,
                operation: entry =>
                {
                    using var _ = new EntryContextScope(ref ctx, entry);
                    return BulkResult(schemeAttributes, 
                        errorMessage: "Failed to validate all values",
                        context: ctx,
                        operation: attribute =>
                    {
                        using var attrScope = new AttributeContextScope(ref ctx, attribute);
                        var entryData = entry.GetData(attribute);
                        if (entryData.Failed)
                        {
                            return entryData.Cast();
                        }

                        var fieldData = entryData.Result;
                        var validateData = attribute.IsValidValue(ctx, fieldData);
                        if (validateData.Failed)
                        {
                            return validateData;
                        }

                        return Pass();
                    });
            });
        }
    }
}