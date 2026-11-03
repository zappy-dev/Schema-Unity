using Schema.Core.Data;

namespace Schema.Core
{
    public class DataTypeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        private readonly DataType previousDataType;

        public DataTypeContextScope(ref SchemaContext context, DataType dataType)
        {
            Context = context;
            if (Context != null)
            {
                previousDataType = Context.DataType;
                Context.DataType = dataType;
            }
        }


        public void Dispose()
        {
            if (Context != null)
            {
                Context.DataType = previousDataType;
            }
            Context = null;
        }
    }
}