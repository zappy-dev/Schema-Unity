using System;

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
    }
}