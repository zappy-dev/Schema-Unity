namespace Schema.Core
{
    public class DataTypeContextScope : ContextScope
    {
        public DataTypeContextScope(ref SchemaContext context, string dataType) : base(ref context)
        {
            Context.DataType = dataType;
        }


        public override void Dispose()
        {
            Context.DataType = string.Empty;
        }
    }
}