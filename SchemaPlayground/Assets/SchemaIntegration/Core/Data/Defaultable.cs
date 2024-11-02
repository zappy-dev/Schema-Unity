using System;

namespace Schema.Core
{
    public abstract class Defaultable
    {
        public object DefaultValue { get; set;  }

        public object CloneDefaultValue()
        {
            var type = DefaultValue.GetType();
            
            // is struct / value copy type
            if (type.IsValueType)
            {
                return DefaultValue;
            }

            if (DefaultValue is ICloneable cloneable)
            {
                return cloneable.Clone();
            }

            throw new InvalidOperationException("Cloning of " + type.FullName + " is not implemented.");
        }
    }
}