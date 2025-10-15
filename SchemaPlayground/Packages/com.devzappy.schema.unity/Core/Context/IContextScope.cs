using System;

namespace Schema.Core
{
    internal interface IContextScope : IDisposable
    {
        public SchemaContext Context { get; set; }
    }
}