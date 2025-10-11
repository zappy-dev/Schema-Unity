using System;
using Schema.Core.Data;

namespace Schema.Core
{
    public class SchemeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        public SchemeContextScope(ref SchemaContext context, DataScheme scheme)
        {
            Context = context;
            context.Scheme = scheme;
        }
        
        public void Dispose()
        {
            Context.Scheme = null;
            Context = null;
        }
    }
}