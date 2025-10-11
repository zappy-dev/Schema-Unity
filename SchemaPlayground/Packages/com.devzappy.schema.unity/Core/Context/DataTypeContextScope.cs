using Schema.Core.Data;

namespace Schema.Core
{
    public class DataTypeContextScope : IContextScope
    {
        public SchemaContext Context { get; set; }
        public DataTypeContextScope(ref SchemaContext context, DataType dataType)
        {
            Context = context;
            Context.DataType = dataType;
        }


        public void Dispose()
        {
            Context.DataType = null;
            Context = null;
        }
    }
}