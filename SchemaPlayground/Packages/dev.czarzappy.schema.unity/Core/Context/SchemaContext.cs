using System.Text;
using Schema.Core.Data;

namespace Schema.Core
{
    public struct SchemaContext
    {
        public DataScheme Scheme;
        public string AttributeName;
        public string DataType;
        public string Driver;

        public bool IsEmpty => string.IsNullOrEmpty(AttributeName) && 
                               string.IsNullOrEmpty(DataType) && 
                               string.IsNullOrEmpty(Driver) && Scheme == null;

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Driver))
            {
                sb.Append($"[Driver={Driver}]");
            }
            if (Scheme != null)
            {
                sb.Append($"[Scheme={Scheme}]");
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
                Scheme = ctxA.Scheme ?? ctxB.Scheme,
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