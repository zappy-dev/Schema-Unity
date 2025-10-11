using Schema.Core.Data;

namespace Schema.Core
{
    public struct AttributeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        
        public AttributeContextScope(ref SchemaContext context, string attrName)
        {
            this.Context = context;
            Context.AttributeName = attrName;
        }
        
        public AttributeContextScope(ref SchemaContext context, AttributeDefinition attribute) : this(ref context, attribute.AttributeName)
        {
        }

        public void Dispose()
        {
            Context.AttributeName = null;
            Context = null;
        }
    }
}