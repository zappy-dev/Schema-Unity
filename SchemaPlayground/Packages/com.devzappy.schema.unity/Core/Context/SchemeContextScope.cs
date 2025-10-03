using System;
using Schema.Core.Data;

namespace Schema.Core
{
    public class SchemeContextScope : ContextScope
    {
        public SchemeContextScope(ref SchemaContext context, DataScheme scheme) : base(ref context) 
        {
            context.Scheme = scheme;
        }
        
        public override void Dispose()
        {
            Context.Scheme = null;
        }
    }
}