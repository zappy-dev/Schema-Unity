using System;

namespace Schema.Core
{
    public sealed class DataType
    {
        public static readonly DataType String = new DataType("String", string.Empty);
        public static readonly DataType Integer = new DataType("Integer", 0);
        public static readonly DataType DateTime = new DataType("Date Time", System.DateTime.Now);

        public static readonly DataType[] BuiltInTypes = {
            String,
            Integer,
            DateTime
        };
        
        public string TypeName { get; private set;  }
        
        public object DefaultValue { get; private set;  }

        private DataType(string typeName, object defaultValue)
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