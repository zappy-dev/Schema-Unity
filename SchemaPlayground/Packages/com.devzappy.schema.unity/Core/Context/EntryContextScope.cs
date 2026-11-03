using System;
using Schema.Core.Data;

namespace Schema.Core
{
    public class EntryContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        private readonly DataEntry previousEntry;

        public EntryContextScope(ref SchemaContext ctx, DataEntry entry)
        {
            Context = ctx;
            if (Context != null)
            {
                previousEntry = Context.Entry;
                Context.Entry = entry;
            }
        }

        public void Dispose()
        {
            if (Context != null)
            {
                Context.Entry = previousEntry;
            }
            Context = null;
        }
    }
}