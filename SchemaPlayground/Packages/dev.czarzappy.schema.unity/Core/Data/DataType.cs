using System;
using System.Linq;
using Schema.Core.Serialization;

namespace Schema.Core.Data
{
    [Serializable]
    public abstract class DataType : Defaultable
    {
        public static readonly DataType String = new  TextDataType();
        public static readonly DataType FilePath = new FilePathDataType();
        public static readonly DataType Integer = new IntegerDataType();
        public static readonly DataType DateTime = new DateTimeDataType();

        public static readonly DataType Default = String;
        
        public static readonly DataType[] BuiltInTypes = {
            String,
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

        public static bool TryToConvertData(object entryData, DataType fromType, DataType toType, out object convertedData)
        {
            Logger.LogVerbose($"Trying to convert {entryData} to {toType}", "DataConversion");
            try
            {
                // Handle unknown types from their default string values
                bool isUnknownType = !BuiltInTypes.Contains(fromType);

                // passthrough if type data isn't converting
                if (fromType == toType)
                {
                    convertedData = entryData;
                    return true;
                }

                if (fromType.Equals(String) || isUnknownType)
                {
                    string data = entryData?.ToString();

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
                            if (refDataType.IsValid(data))
                            {
                                convertedData = data;
                                return true;
                            }
                            
                            convertedData = null;
                            return false;
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
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to convert '{entryData}' from {fromType} into {toType}, error: {e.Message}");
            }

            // unhandled type conversion
            convertedData = null;
            return false;
        }
        
        public abstract bool IsValid(object value);
    }
}