using System;
using System.IO;
using System.Linq;

namespace Schema.Core
{
    [Serializable]
    public class DataType : Defaultable
    {
        public static readonly DataType String = new DataType("String", string.Empty);
        public static readonly DataType String_FilePath = new DataType("String/File Path", string.Empty);
        public static readonly DataType Integer = new DataType("Integer", 0);
        public static readonly DataType DateTime = new DataType("Date Time", System.DateTime.Today);

        public static readonly DataType[] BuiltInTypes = {
            String,
            String_FilePath,
            Integer,
            DateTime,
        };

        public static bool TryToConvertData(object entryData, DataType fromType, DataType toType, out object convertedData)
        {
            if (fromType.Equals(String))
            {
                string data = (string)entryData;
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
                    convertedData = System.DateTime.Parse(data);
                    return true;
                }

                if (toType.Equals(String_FilePath))
                {
                    if (!File.Exists(data))
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
                    if (!Schema.TryGetSchema(refDataType.ReferenceSchemaName, out var refSchema))
                    {
                        Logger.LogWarning($"Reference schema '{refDataType.ReferenceSchemaName}' does not exist");
                        convertedData = null;
                        return false;
                    }

                    if (!refSchema.TryGetIdentifierAttribute(out var identifier))
                    {
                        Logger.LogWarning("Reference schema does not have an identifier attribute");
                        convertedData = null;
                        return false;
                    }

                    if (identifier.AttributeName != refDataType.ReferenceAttributeName)
                    {
                        Logger.LogWarning($"Reference schema identifier {identifier} does not match {refDataType}");
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

            if (toType.Equals(String))
            {
                convertedData = entryData == null ? "" : entryData.ToString();
                return true;
            }

            // unhandled type conversion
            convertedData = null;
            return false;
        }
        
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