using System;
using Schema.Core.Data;

namespace Schema.Core
{
    public class EntryContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        public EntryContextScope(ref SchemaContext ctx, DataEntry entry)
        {
            Context = ctx;
            if (Context != null)
            {
                Context.Entry = entry;
            }
        }

        public void Dispose()
        {
            if (Context != null)
            {
                Context.Entry = null;
            }
            Context = null;
        }
    }
}