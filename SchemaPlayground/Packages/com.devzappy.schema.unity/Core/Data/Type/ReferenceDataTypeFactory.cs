using System.Linq;

namespace Schema.Core.Data
{
    public static class ReferenceDataTypeFactory
    {
        public static SchemaResult<ReferenceDataType> CreateReferenceDataType(SchemaContext ctx, 
            string referenceSchemeName, 
            string identifierAttribute, 
            bool validateSchemeLoaded = true)
        {
            var res = SchemaResult<ReferenceDataType>.New(ctx);
            
            object defaultValue = string.Empty;
            // Set an initial default value
            if (validateSchemeLoaded)
            {
                if (Schema.GetScheme(ctx, referenceSchemeName).TryErr(out var refScheme, out var refErr))
                    return refErr.CastError(res);
                if (refScheme.GetIdentifierAttribute().TryErr(out var idAttr, out var idErr)) 
                    return idErr.CastError(res);

                if (idAttr.AttributeName != identifierAttribute) 
                    return res.Fail($"Referenced ID attribute does not match, given: {identifierAttribute}, found: {idAttr.AttributeName}");

                defaultValue = idAttr.CloneDefaultValue();
                    
                // TODO: If the referenced data scheme doesn't have id values, is it valid to reference anything that doesn't exist?
                // Assuming the next new value will be the default value..
                var values = refScheme.GetIdentifierValues().Select(v => v?.ToString() ?? "").ToList();
                if (values.Count > 0)
                {
                    defaultValue = values[0];
                }
            }
            
            return res.Pass(new ReferenceDataType(referenceSchemeName, identifierAttribute, defaultValue));
        }
    }
}