using Schema.Core.Data;

namespace Schema.Core
{
    public struct AttributeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        private string previousAttributeName;
        
        public AttributeContextScope(ref SchemaContext context, string attrName)
        {
            this.Context = context;
            this.previousAttributeName = null;
            if (Context != null)
            {
                previousAttributeName = Context.AttributeName;
                Context.AttributeName = attrName;
            }
        }
        
        public AttributeContextScope(ref SchemaContext context, AttributeDefinition attribute) : this(ref context, attribute.AttributeName)
        {
        }

        public void Dispose()
        {
            if (Context != null)
            {
                Context.AttributeName = previousAttributeName;
            }
            Context = null;
        }
    }
}