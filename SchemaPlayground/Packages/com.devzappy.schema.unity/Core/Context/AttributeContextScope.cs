using Schema.Core.Data;

namespace Schema.Core
{
    public struct AttributeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        
        public AttributeContextScope(ref SchemaContext context, string attrName)
        {
            this.Context = context;
            if (Context != null)
            {
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
                Context.AttributeName = null;
            }
            Context = null;
        }
    }
}