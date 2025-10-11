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
            Context.Entry = entry;
        }

        public void Dispose()
        {
            Context.Entry = null;
            Context = null;
        }
    }
}