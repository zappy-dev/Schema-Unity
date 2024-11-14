using System;

namespace Schema.Core.Data
{
    public abstract class Defaultable
    {
        public object DefaultValue { get; set;  }

        public Defaultable(object defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public Defaultable()
        {
            
        }

        public object CloneDefaultValue()
        {
            if (DefaultValue == null)
            {
                return null;
            }
            
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