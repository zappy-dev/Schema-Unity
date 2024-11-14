using System;

namespace Schema.Core.Data
{
    public struct AttributeSortOrder : IEquatable<AttributeSortOrder>
    {
        public static readonly AttributeSortOrder None = new AttributeSortOrder(default, SortOrder.None);
        public string AttributeName;
        public SortOrder Order;

        public AttributeSortOrder(string attributeName, SortOrder order)
        {
            AttributeName = attributeName;
            Order = order;
        }

        public bool HasValue => !string.IsNullOrEmpty(AttributeName) && Order != SortOrder.None;

        public bool Equals(AttributeSortOrder other)
        {
            return AttributeName == other.AttributeName && Order == other.Order;
        }

        public override bool Equals(object obj)
        {
            return obj is AttributeSortOrder other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((AttributeName != null ? AttributeName.GetHashCode() : 0) * 397) ^ (int)Order;
            }
        }

        public static bool operator ==(AttributeSortOrder left, AttributeSortOrder right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttributeSortOrder left, AttributeSortOrder right)
        {
            return !left.Equals(right);
        }
    }
}