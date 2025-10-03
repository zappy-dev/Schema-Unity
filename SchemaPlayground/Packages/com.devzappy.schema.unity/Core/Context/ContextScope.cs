using System;

namespace Schema.Core
{
    public abstract class ContextScope : IDisposable
    {
        protected SchemaContext Context;

        protected ContextScope(ref SchemaContext context)
        {
            Context = context;
        }

        public abstract void Dispose();
    }
}