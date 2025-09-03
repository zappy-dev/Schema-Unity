using System;
using System.Linq;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    /// <summary>
    /// Represents an abstract base class for all data types used in the schema system.
    /// Provides built-in types, conversion, and validation logic for schema data.
    /// </summary>
    [Serializable]
    public abstract class DataType : Defaultable
    {
        /// <summary>
        /// Built-in text data type.
        /// </summary>
        public static readonly DataType Text = new TextDataType();
        /// <summary>
        /// Built-in file path data type.
        /// </summary>
        public static readonly DataType FilePath = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true);
        /// <summary>
        /// Built-in guid data type
        /// </summary>
        public static readonly DataType Guid = new GuidDataType();
        /// <summary>
        /// Built-in integer data type.
        /// </summary>
        public static readonly DataType Integer = new IntegerDataType();
        /// <summary>
        /// Built-in integer data type.
        /// </summary>
        public static readonly DataType Float = new FloatingPointDataType();
        /// <summary>
        /// Built-in date/time data type.
        /// </summary>
        public static readonly DataType DateTime = new DateTimeDataType();

        /// <summary>
        /// Built-in boolean data type.
        /// </summary>
        public static readonly DataType Boolean = new BooleanDataType();

        /// <summary>
        /// The default data type (Text).
        /// </summary>
        public static readonly DataType Default = Text;
        
        /// <summary>
        /// Array of all built-in data types.
        /// </summary>
        public static readonly DataType[] BuiltInTypes = {
            Text,
            FilePath,
            Guid,
            Integer,
            Float,
            DateTime,
            Boolean,
        };

        /// <summary>
        /// Gets the name of the data type.
        /// </summary>
        public abstract string TypeName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataType"/> class.
        /// </summary>
        internal DataType()
        {
            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataType"/> class with a default value.
        /// </summary>
        /// <param name="defaultValue">The default value for the data type.</param>
        internal DataType(object defaultValue) : this()
        {
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Returns a string representation of the data type.
        /// </summary>
        /// <returns>A string describing the data type.</returns>
        public override string ToString()
        {
            return $"DataType: {TypeName}";
        }

        private bool Equals(DataType other)
        {
            return TypeName.Equals(other.TypeName);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is DataType other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (TypeName != null ? TypeName.GetHashCode() : 0);
        }

        /// <summary>
        /// Determines whether two <see cref="DataType"/> instances are equal.
        /// </summary>
        public static bool operator ==(DataType left, DataType right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Determines whether two <see cref="DataType"/> instances are not equal.
        /// </summary>
        public static bool operator !=(DataType left, DataType right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Converts data from one data type to another, handling built-in and unknown types.
        /// </summary>
        /// <param name="entryData">The data to convert.</param>
        /// <param name="fromType">The source data type.</param>
        /// <param name="toType">The target data type.</param>
        /// <returns>A <see cref="SchemaResult{object}"/> representing the conversion result.</returns>
        public static SchemaResult<object> ConvertData(object entryData, DataType fromType, DataType toType, SchemaContext context)
        {
            Logger.LogDbgVerbose($"Trying to convert {entryData} to {toType}", "DataConversion");
            // Handle unknown types from their default string values
            bool isUnknownType = !BuiltInTypes.Contains(fromType);

            // passthrough if type data isn't converting
            if (fromType == toType)
            {
                return SchemaResult<object>.Pass(entryData,
                    successMessage: "Conversion no-op for matching type", Schema.Context.DataConversion);
            }

            if (fromType.Equals(Text) || isUnknownType)
            {
                string data = entryData?.ToString();

                if (string.IsNullOrWhiteSpace(data))
                {
                    return SchemaResult<object>.Pass(toType.CloneDefaultValue(),
                        successMessage: "Converted empty data to default value", Schema.Context.DataConversion);
                }

                return toType.ConvertData(data, context);
            }

            return toType.ConvertData(entryData, context);
        }

        /// <summary>
        /// Checks if the provided value is valid for this data type.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>A <see cref="SchemaResult"/> indicating if the value is valid.</returns>
        public abstract SchemaResult CheckIfValidData(object value, SchemaContext context);
        
        /// <summary>
        /// Converts the provided value to this data type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="SchemaResult{object}"/> representing the conversion result.</returns>
        public abstract SchemaResult<object> ConvertData(object value, SchemaContext context);
    }
}