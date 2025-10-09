using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Schema.Core.Data
{
    [Serializable]
    public class ListDataType : DataType
    {
        public override string TypeName => ElementType != null ? $"List of {ElementType.TypeName}" : "List";
        
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute)
        {
            // Generate code like: DataEntry.GetDataAsList<int>("attributeName")
            var elementTypeName = GetElementTypeForCode();
            return SchemaResult<string>.Pass($"{nameof(DataEntry)}.GetDataAsList<{elementTypeName}>(\"{attribute.AttributeName}\")");
        }
        
        public override string CSDataType
        {
            get
            {
                var elementTypeName = GetElementTypeForCode();
                return $"System.Collections.Generic.List<{elementTypeName}>";
            }
        }
        
        public DataType ElementType { get; set; }

        public ListDataType()
        {
            DefaultValue = new object[] { };
        }
        
        public ListDataType(DataType elementType, SchemaContext context) : 
            base(GenerateDefaultValue(elementType, context).Result) // HACK: lots of work in this parameter to a base constructor
        {
            ElementType = elementType;
        }

        private string GetElementTypeForCode()
        {
            if (ElementType == null)
                return "object";
                
            // Use the CSDataType property from the element type
            return ElementType.CSDataType;
        }

        private static SchemaResult<object> GenerateDefaultValue(DataType elementType, SchemaContext context)
        {
            if (context.IsEmpty)
            {
                throw new InvalidOperationException();// try to prevent Json serialization using this path
            }
            using var _ = new DataTypeContextScope(ref context, $"List of {elementType.TypeName}");
            
            switch (elementType)
            {
                case TextDataType _:
                case FSDataType _:
                    return SchemaResult<object>.Pass(new string[] { });
                case IntegerDataType _:
                    return SchemaResult<object>.Pass(new int[] { });
                case GuidDataType _:
                    return SchemaResult<object>.Pass(new Guid[] { });
                case BooleanDataType _:
                    return SchemaResult<object>.Pass(new bool[] { });
                case DateTimeDataType _:
                    return SchemaResult<object>.Pass(new DateTime[] { });
                case FloatingPointDataType _:
                    return SchemaResult<object>.Pass(new float[] { });
                case ReferenceDataType referenceDataType:
                    if (!referenceDataType.GetReferencedIdentifierAttribute(context)
                        .Try(out var idAttr, out var idAttrError))
                    {
                        return idAttrError.CastError<object>();
                    }
                    
                    return GenerateDefaultValue(idAttr.DataType, context);
                default:
                    return SchemaResult<object>.Pass(new object[] { });
            }
        }

        public override object Clone()
        {
            return new ListDataType
            {
                DefaultValue = DefaultValue,
                ElementType = ElementType?.Clone() as DataType
            };
        }

        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            
            if (value == null)
            {
                return Fail("Value is null", context);
            }
            
            if (!(value is IEnumerable enumerable))
            {
                return Fail($"Value is not enumerable. Type: {value.GetType()}", context);
            }

            if (value is string)
            {
                return Fail("Value is a string, not a list", context);
            }

            if (ElementType == null)
            {
                return Pass("ElementType is null, accepting any enumerable", context);
            }

            // Check each element in the list
            var enumerator = enumerable.GetEnumerator();
            int index = 0;
            while (enumerator.MoveNext())
            {
                var element = enumerator.Current;
                var elementValidation = ElementType.IsValidValue(context, element);
                
                if (elementValidation.Failed)
                {
                    return Fail($"Element at index {index} is invalid: {elementValidation.Message}", context);
                }
                index++;
            }
            
            return Pass($"All {index} elements are valid", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            
            if (value == null)
            {
                return Pass<object>(CloneDefaultValue(), "Converted null to default value", context);
            }
            
            // Handle JArray from JSON deserialization
            if (value is JArray jArray)
            {
                value = jArray.ToObject<object[]>();
            }

            if (!(value is IEnumerable enumerable))
            {
                return Fail<object>($"Cannot convert '{value}' to list - not enumerable", context);
            }

            if (value is string str)
            {
                if (ElementType is TextDataType textType)
                {
                    return Fail<object>($"Cannot convert string '{str}' to List<{ElementType}>", context);
                }
                else
                {
                    return Pass<object>(new List<string>
                    {
                        str,
                    });
                }
            }

            if (ElementType == null)
            {
                // Convert to object array if no element type specified
                var objectList = new List<object>();
                foreach (var item in enumerable)
                {
                    objectList.Add(item);
                }
                return Pass<object>(objectList.ToArray(), "Converted to object array", context);
            }

            // Convert each element to the target element type
            var convertedList = new List<object>();
            int index = 0;
            foreach (var item in enumerable)
            {
                var convertResult = ElementType.ConvertValue(context, item);
                if (convertResult.Failed)
                {
                    return Fail<object>($"Failed to convert element at index {index}: {convertResult.Message}", context);
                }
                convertedList.Add(convertResult.Result);
                index++;
            }

            // Convert to typed array based on element type
            object typedArray;
            switch (ElementType)
            {
                case TextDataType _:
                case FSDataType _:
                    typedArray = convertedList.Cast<string>().ToArray();
                    break;
                case IntegerDataType _:
                    typedArray = convertedList.Select(o => Convert.ToInt32(o)).ToArray();
                    break;
                case FloatingPointDataType _:
                    typedArray = convertedList.Select(o => Convert.ToSingle(o)).ToArray();
                    break;
                case BooleanDataType _:
                    typedArray = convertedList.Cast<bool>().ToArray();
                    break;
                case DateTimeDataType _:
                    typedArray = convertedList.Cast<DateTime>().ToArray();
                    break;
                case GuidDataType _:
                    typedArray = convertedList.Cast<Guid>().ToArray();
                    break;
                case ReferenceDataType _:
                    // References are stored as their identifier type
                    typedArray = convertedList.ToArray();
                    break;
                default:
                    typedArray = convertedList.ToArray();
                    break;
            }

            return Pass<object>(typedArray, $"Converted list with {convertedList.Count} elements", context);
        }

        private bool Equals(ListDataType other)
        {
            if (other == null) return false;
            if (ElementType == null && other.ElementType == null) return true;
            if (ElementType == null || other.ElementType == null) return false;
            return ElementType.Equals(other.ElementType);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ListDataType)obj);
        }

        public override int GetHashCode()
        {
            return (TypeName != null ? TypeName.GetHashCode() : 0);
        }
    }
}