using System;
using System.Linq;
using Schema.Core.Serialization;

namespace Schema.Core
{
    [Serializable]
    public class DataType : Defaultable
    {
        public static readonly DataType String = new DataType(nameof(String), string.Empty);
        public static readonly DataType FilePath = new DataType(nameof(FilePath), string.Empty);
        public static readonly DataType Integer = new DataType(nameof(Integer), 0);
        public static readonly DataType DateTime = new DataType(nameof(DateTime), System.DateTime.Today);

        public static readonly DataType[] BuiltInTypes = {
            String,
            FilePath,
            Integer,
            DateTime,
        };

        public static bool TryToConvertData(object entryData, DataType fromType, DataType toType, out object convertedData)
        {
            Logger.LogVerbose($"Trying to convert {entryData} to {toType}");
            // Handle unknown types from their default string values
            bool isUnknownType = !BuiltInTypes.Contains(fromType);

            if (fromType.Equals(String) || isUnknownType)
            {
                string data = (string)entryData;
                
                try
                {
                    if (string.IsNullOrEmpty(data))
                    {
                        convertedData = toType.CloneDefaultValue();
                        return true;
                    }

                    if (toType.Equals(Integer))
                    {
                        convertedData = Convert.ToInt32(data);
                        return true;
                    }
                    if (toType.Equals(DateTime))
                    {
                        bool result = System.DateTime.TryParse(data, out var date);
                        convertedData = date;
                        return result;
                    }

                    if (toType.Equals(FilePath))
                    {
                        if (!Storage.FileSystem.FileExists(data))
                        {
                            Logger.LogWarning($"File {data} does not exist");
                            convertedData = null;
                            return false;
                        }

                        convertedData = data;
                        return true;
                    }

                    if (toType is ReferenceDataType refDataType)
                    {
                        if (!Schema.TryGetScheme(refDataType.ReferenceSchemeName, out var refSchema))
                        {
                            Logger.LogWarning($"Could not load Reference Scheme named '{refDataType.ReferenceSchemeName}'");
                            convertedData = null;
                            return false;
                        }

                        if (!refSchema.TryGetIdentifierAttribute(out var identifier))
                        {
                            Logger.LogWarning("Reference Scheme has no Attribute marked as Identifier.");
                            convertedData = null;
                            return false;
                        }

                        if (identifier.AttributeName != refDataType.ReferenceAttributeName)
                        {
                            Logger.LogWarning($"Reference Scheme identifier {identifier} does not match {refDataType}");
                            convertedData = null;
                            return false;
                        }

                        bool identifierExist = refSchema.GetIdentifierValues().Any(v => v.Equals(data));
                        if (!identifierExist)
                        {
                            Logger.LogWarning($"Value '{data}' does not exist as an identifier in {refDataType}");
                        }
                        convertedData = identifierExist ? data : null;
                        return identifierExist;
                    }

                    Logger.LogError($"Unable to convert data from '{data}' of type '{fromType}' to '{toType}'");
                    // unhandled string type conversion
                    convertedData = null;
                    return false;
                }
                catch (FormatException e)
                {
                    Logger.LogError($"Unable to convert '{data}' into {toType}, error: {e.Message}");
                    convertedData = null;
                    return false;
                }
            }

            if (toType.Equals(String))
            {
                // todo, handle formating for DateTimes explicitly
                convertedData = entryData == null ? "" : entryData.ToString();
                return true;
            }

            // unhandled type conversion
            convertedData = null;
            return false;
        }
        
        public string TypeName { get; set;  }

        internal DataType()
        {
            
        }

        internal DataType(string typeName, object defaultValue) : this()
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