namespace Schema.Core
{
    public class AttributeContextScope : ContextScope
    {
        public AttributeContextScope(ref SchemaContext context, string attrName) : base(ref context)
        {
            Context.AttributeName = attrName;
        }

        public override void Dispose()
        {
            Context.AttributeName = null;
        }
    }
}