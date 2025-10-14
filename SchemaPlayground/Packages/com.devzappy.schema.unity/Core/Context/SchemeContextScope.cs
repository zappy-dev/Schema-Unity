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
            if (context != null)
            {
                context.Scheme = scheme;
            }
        }
        
        public void Dispose()
        {
            if (Context != null)
            {
                Context.Scheme = null;
            }
            Context = null;
        }
    }
}