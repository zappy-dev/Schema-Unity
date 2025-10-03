using System;
using System.Text;
using Schema.Core.Data;

namespace Schema.Core
{
    public struct SchemaContext : IEquatable<SchemaContext>
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

        public bool Equals(SchemaContext other)
        {
            return Equals(Scheme, other.Scheme) && AttributeName == other.AttributeName && DataType == other.DataType && Driver == other.Driver;
        }

        public override bool Equals(object obj)
        {
            return obj is SchemaContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Scheme != null ? Scheme.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AttributeName != null ? AttributeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DataType != null ? DataType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Driver != null ? Driver.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}