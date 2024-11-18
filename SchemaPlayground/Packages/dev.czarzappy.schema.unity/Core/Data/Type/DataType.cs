using System;
using System.Linq;

namespace Schema.Core.Data
{
    [Serializable]
    public abstract class DataType : Defaultable
    {
        public static readonly DataType Text = new TextDataType();
        public static readonly DataType FilePath = new FilePathDataType();
        public static readonly DataType Integer = new IntegerDataType();
        public static readonly DataType DateTime = new DateTimeDataType();

        public static readonly DataType Default = Text;
        
        public static readonly DataType[] BuiltInTypes = {
            Text,
            FilePath,
            Integer,
            DateTime,
        };
        
        public abstract string TypeName { get; }

        internal DataType()
        {
            
        }

        internal DataType(object defaultValue) : this()
        {
            DefaultValue = defaultValue;
        }

        public override string ToString()
        {
            return $"DataType: {TypeName}";
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

        public static SchemaResult<object> ConvertData(object entryData, DataType fromType, DataType toType)
        {
            Logger.LogVerbose($"Trying to convert {entryData} to {toType}", "DataConversion");
            // Handle unknown types from their default string values
            bool isUnknownType = !BuiltInTypes.Contains(fromType);

            // passthrough if type data isn't converting
            if (fromType == toType)
            {
                return SchemaResult<object>.Pass(entryData,
                    successMessage: $"Conversion no-op for matching type {fromType}", Schema.Context.DataConversion);
            }

            if (fromType.Equals(Text) || isUnknownType)
            {
                string data = entryData?.ToString();

                if (string.IsNullOrWhiteSpace(data))
                {
                    return SchemaResult<object>.Pass(toType.CloneDefaultValue(),
                        successMessage: $"Converted empty data to default value", Schema.Context.DataConversion);
                }

                return toType.ConvertData(data);
            }

            return toType.ConvertData(entryData);
        }

        public abstract SchemaResult CheckIfValidData(object value);
        
        public abstract SchemaResult<object> ConvertData(object value);
    }
}