using System.Text;

namespace Schema.Core
{
    public struct SchemaContext
    {
        public string SchemeName;
        public string AttributeName;
        public string DataType;
        public string Driver;

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Driver))
            {
                sb.Append($"[Driver={Driver}]");
            }
            if (!string.IsNullOrEmpty(SchemeName))
            {
                sb.Append($"[SchemeName={SchemeName}]");
            }
            if (!string.IsNullOrEmpty(AttributeName))
            {
                sb.Append($"[AttributeName={AttributeName}]");
            }
            if (!string.IsNullOrEmpty(DataType))
            {
                sb.Append($"[DataType={DataType}]");
            }

            return sb.ToString();
        }

        public static SchemaContext Merge(SchemaContext ctxA, SchemaContext ctxB)
        {
            return new SchemaContext
            {
                SchemeName = (string.IsNullOrEmpty(ctxA.SchemeName) ? ctxB.SchemeName : ctxA.SchemeName),
                AttributeName = (string.IsNullOrEmpty(ctxA.AttributeName) ? ctxB.AttributeName : ctxA.AttributeName),
                DataType = (string.IsNullOrEmpty(ctxA.DataType) ? ctxB.DataType : ctxA.DataType),
            };
        }
        
        public static SchemaContext operator | (SchemaContext left, SchemaContext right)
        {
            return Merge(left, right);
        }
    }
}