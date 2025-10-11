using System;
using System.Text;
using Schema.Core.Data;

namespace Schema.Core
{
    public class SchemaContext : IEquatable<SchemaContext>
    {
        public DataScheme Scheme { get; set; }
        public string AttributeName { get; set; }
        public DataType DataType { get; set; }
        public string Driver;
        public DataEntry Entry { get; internal set; }

        public bool IsEmpty => string.IsNullOrEmpty(AttributeName) && 
                               DataType == null && 
                               string.IsNullOrEmpty(Driver) && 
                               Scheme == null && 
                               Entry == null;

        public override string ToString()
        {
            var sb = new StringBuilder();
            int rightPad = 15;
            if (!string.IsNullOrEmpty(Driver))
            {
                sb.Append($"- Driver:".PadRight(rightPad));
                sb.AppendLine(Driver);
            }
            if (Scheme != null)
            {
                sb.Append($"- Scheme:".PadRight(rightPad));
                sb.AppendLine(Scheme.ToString());
            }
            if (!string.IsNullOrEmpty(AttributeName))
            {
                sb.Append($"- AttributeName:".PadRight(rightPad));
                sb.AppendLine(AttributeName);
            }
            if (DataType != null)
            {
                sb.Append($"- DataType:".PadRight(rightPad));
                sb.AppendLine(DataType.ToString());
            }
            if (Entry != null)
            {
                sb.Append($"- Entry:".PadRight(rightPad));
                sb.AppendLine(Entry.ToString());
            }

            return sb.ToString();
        }

        public static SchemaContext Merge(SchemaContext ctxA, SchemaContext ctxB)
        {
            return new SchemaContext
            {
                Scheme = ctxA.Scheme ?? ctxB.Scheme,
                Entry = ctxA.Entry ?? ctxB.Entry,
                DataType = ctxA.DataType ?? ctxB.DataType,
                AttributeName = (string.IsNullOrEmpty(ctxA.AttributeName) ? ctxB.AttributeName : ctxA.AttributeName),
            };
        }
        
        public static SchemaContext operator | (SchemaContext left, SchemaContext right)
        {
            return Merge(left, right);
        }

        public bool Equals(SchemaContext other)
        {
            return Equals(Scheme, other.Scheme) && 
                   Equals(Entry, other.Entry) && 
                   AttributeName == other.AttributeName && 
                   DataType == other.DataType && 
                   Driver == other.Driver;
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
                hashCode = (hashCode * 397) ^ (Entry != null ? Entry.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AttributeName != null ? AttributeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DataType != null ? DataType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Driver != null ? Driver.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}