using System;
using Schema.Core.Data;

namespace Schema.Core
{
    public class SchemeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        private readonly DataScheme previousScheme;

        public SchemeContextScope(ref SchemaContext context, DataScheme scheme)
        {
            Context = context;
            if (context != null)
            {
                previousScheme = context.Scheme;
                context.Scheme = scheme;
            }
        }
        
        public void Dispose()
        {
            if (Context != null)
            {
                Context.Scheme = previousScheme;
            }
            Context = null;
        }
    }
}