using Schema.Core.Data;

namespace Schema.Core
{
    public class DataTypeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        public DataTypeContextScope(ref SchemaContext context, DataType dataType)
        {
            Context = context;
            if (Context != null)
            {
                Context.DataType = dataType;
            }
        }


        public void Dispose()
        {
            if (Context != null)
            {
                Context.DataType = null;
            }
            Context = null;
        }
    }
}