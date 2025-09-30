using System;
using System.Collections;
using System.Collections.Generic;

namespace Schema.Core.Data
{
    public class ListDataType : DataType
    {
        public override string TypeName => $"List of {ElementType.TypeName}";
        public DataType ElementType { get; private set; }

        public ListDataType()
        {
            
        }
        
        public ListDataType(DataType elementType, SchemaContext context) : 
            base(GenerateDefaultValue(elementType, context).Result) // HACK: lots of work in this parameter to a base constructor
        {
            ElementType = elementType;
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
                    // if (context.IsEmpty)
                    // {
                    //     return SchemaResult<object>.Pass(new object[] {});
                    // }
                    if (!referenceDataType.GetReferencedIdentifierAttribute(context)
                        .Try(out var idAttr, out var idAttrError))
                    {
                        return idAttrError.CastError<object>();
                    }
                    
                    return GenerateDefaultValue(idAttr.DataType, context);
                default:
                    return SchemaResult<object>.Fail($"No default value generator for list of given type: {elementType}", context);
                    
            }
        }

        public override object Clone()
        {
            throw new System.NotImplementedException();
        }

        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            if (!(value is IEnumerable enumerable))
            {
                return Fail("Value is not Enumerable", context);
            }

            if (enumerable is object[] objArray)
            {
                if (objArray.Length == 0)
                {
                    return Pass("Value is empty object array", context);
                }
            }
            
            return Fail("Not implemented", context);
        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object value)
        {
            return Fail<object>("Not implemented");
        }
    }
}