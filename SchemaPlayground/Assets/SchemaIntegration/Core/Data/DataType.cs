using System;
using System.Collections.Generic;

namespace Schema.Core
{
    [Serializable]
    public sealed class DataType : Defaultable
    {
        public static readonly DataType String = new DataType("String", string.Empty);
        public static readonly DataType Integer = new DataType("Integer", 0);
        public static readonly DataType DateTime = new DataType("Date Time", System.DateTime.Today);

        public static readonly DataType[] BuiltInTypes = {
            String,
            Integer,
            DateTime
        };
        
        public string TypeName { get; set;  }

        public DataType()
        {
            
        }

        private DataType(string typeName, object defaultValue) : this()
        {
            TypeName = typeName;
            DefaultValue = defaultValue;
        }

        public override string ToString()
        {
            return TypeName;
        }

        private bool Equals(DataType other)
        {
            return TypeName.Equals(other.TypeName);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is DataType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (TypeName != null ? TypeName.GetHashCode() : 0);
        }

        public static bool operator ==(DataType left, DataType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DataType left, DataType right)
        {
            return !Equals(left, right);
        }
    }
}