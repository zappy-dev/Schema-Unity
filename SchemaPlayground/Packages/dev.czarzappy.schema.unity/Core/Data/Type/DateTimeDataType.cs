using System;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    [Serializable]
    public class DateTimeDataType : DataType
    {
        public override SchemaContext Context => new SchemaContext()
        {
            DataType = nameof(DateTimeDataType),
        };
        
        public override string TypeName => "Date Time";
        public override SchemaResult CheckIfValidData(object value, SchemaContext context)
        {
            return CheckIf(value is DateTime, 
                errorMessage: "Value is not a DateTime",
                successMessage: "Value is a DateTime", context);

        }

        public override SchemaResult<object> ConvertData(object value, SchemaContext context)
        {
            var data = value as string;
            
            bool result = System.DateTime.TryParse(data, out var date);
            return CheckIf<object>(result, date,
                errorMessage: "Unable to convert value",
                successMessage: "Converted value into DateTime", 
                context: context);
        }

        public DateTimeDataType() : base(System.DateTime.Today)
        {
            
        }

        public DateTimeDataType(DateTime dateTime) : base(dateTime)
        {
            
        }
    }
}