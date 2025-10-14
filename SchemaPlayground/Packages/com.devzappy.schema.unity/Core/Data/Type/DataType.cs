using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    /// <summary>
    /// Represents an abstract base class for all data types used in the schema system.
    /// Provides built-in types, conversion, and validation logic for schema data.
    /// </summary>
    [Serializable]
    public abstract class DataType : Defaultable, ICloneable
    {
        #region Constants
        /// <summary>
        /// Built-in text data type.
        /// </summary>
        public static readonly DataType Text = new TextDataType();
        /// <summary>
        /// Built-in file path data type.
        /// </summary>
        public static readonly DataType FilePath_RelativePaths = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true);
        /// <summary>
        /// Built-in directory path data type.
        /// </summary>
        public static readonly DataType Folder_RelativePaths = new FolderDataType(allowEmptyPath: true, useRelativePaths: true);
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

        #endregion
        
        #region Static Interface

        /// <summary>
        /// Array of all built-in data types.
        /// </summary>
        public static IEnumerable<DataType> BuiltInTypes => CoreBuiltInTypes.Concat(_pluginTypes);

        private static List<DataType> _pluginTypes = new List<DataType>();

        public static void AddPluginType(DataType pluginType)
        {
            _pluginTypes.Add(pluginType);
        }

        public static readonly IReadOnlyList<DataType> CoreBuiltInTypes = new [] {
            Text,
            Boolean,
            Integer,
            Float,
            DateTime,
            Guid,
            FilePath_RelativePaths,
            Folder_RelativePaths,
        };
        // need to separate core data types from dynamically added ones..
        
        #endregion

        #region Interface
        /// <summary>
        /// Gets the name of the data type.
        /// </summary>
        [JsonIgnore]
        public abstract string TypeName { get; }

        public abstract SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute);
        
        [JsonIgnore]
        public abstract string CSDataType { get; }

        public abstract object Clone();

        /// <summary>
        /// Checks if the provided value is valid for this data type.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value">The value to validate.</param>
        /// <returns>A <see cref="SchemaResult"/> indicating if the value is valid.</returns>
        /// <remarks>This is a Hot Path!</remarks>
        public abstract SchemaResult IsValidValue(SchemaContext context, object value);

        /// <summary>
        /// Converts the provided value to this data type.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="SchemaResult{object}"/> representing the conversion result.</returns>
        public abstract SchemaResult<object> ConvertValue(SchemaContext context, object value);

        #endregion

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

        #region Utilities

        /// <summary>
        /// Converts data from one data type to another, handling built-in and unknown types.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entryData">The data to convert.</param>
        /// <param name="fromType">The source data type.</param>
        /// <param name="toType">The target data type.</param>
        /// <returns>A <see cref="SchemaResult{object}"/> representing the conversion result.</returns>
        public static SchemaResult<object> ConvertValue(SchemaContext context, object entryData, DataType fromType,
            DataType toType)
        {
            Logger.LogDbgVerbose($"Trying to convert {entryData} to {toType}", "DataConversion");
            // Handle unknown types from their default string values
            bool isUnknownType = !BuiltInTypes.Contains(fromType);

            // passthrough if type data isn't converting
            if (fromType == toType)
            {
                return SchemaResult<object>.Pass(entryData,
                    successMessage: "Conversion no-op for matching type", context);
            }

            if (fromType.Equals(Text) || isUnknownType)
            {
                string data = entryData?.ToString();

                if (string.IsNullOrWhiteSpace(data))
                {
                    return SchemaResult<object>.Pass(toType.CloneDefaultValue(),
                        successMessage: "Converted empty data to default value", context);
                }

                return toType.ConvertValue(context, data);
            }

            return toType.ConvertValue(context, entryData);
        }

        public static SchemaResult<DataType> InferDataTypeForValues(SchemaContext context, params object[] entryValues)
        {
            var res = SchemaResult<DataType>.New(context);
            // A high quality candidate is a data type which all entry values are valid values for
            var highQualityCandidates = new List<DataType>();
            var potentialDataTypes = new HashSet<DataType>(BuiltInTypes);
            foreach (var rawValue in entryValues)
            {
                var enumerator = potentialDataTypes.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var testType = enumerator.Current;
                    // strings are a default representation for values, deprioritize them
                    bool isHighQualityCandidate = !(testType is TextDataType _);
                    
                    // first check if the given value is already valid for a given data type
                    if (isHighQualityCandidate && testType.IsValidValue(context, rawValue).Try(out var validateErr))
                    {
                        highQualityCandidates.Add(testType);
                        break;
                    }

                    // second, test converting the data and make sure it is valid.
                    if (!testType.ConvertValue(context, rawValue).Try(out var convertedData) ||
                        !testType.IsValidValue(context, convertedData).Passed)
                    {
                        potentialDataTypes.Remove(enumerator.Current);
                    }
                }
            }

            if (highQualityCandidates.Count == 0 && potentialDataTypes.Count == 0)
            {
                return res.Fail("Could not convert all entries to a known data type");
            }

            DataType finalDataType;
            if (highQualityCandidates.Count > 0)
            {
                // TODO: Figure out how to handle multiple high-quality candidates..
                return res.Pass(highQualityCandidates[0]);
            }
            
            if (potentialDataTypes.Count >= 2)
            {
                // prefer non-text data type if possible
                finalDataType = potentialDataTypes.First(dataType => !dataType.Equals(Text));
            }
            else
            {
                finalDataType = potentialDataTypes.First();
            }

            return res.Pass(finalDataType);
        }
        
        #endregion
    }
}