using System;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    [Serializable]
    public class DateTimeDataType : DataType
    {
        public override string TypeName => "Date Time";
        public override object Clone()
        {
            return new DateTimeDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public override SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            return CheckIf(value is DateTime, 
                errorMessage: "Value is not a DateTime",
                successMessage: "Value is a DateTime", context);

        }

        public override SchemaResult<object> ConvertData(SchemaContext context, object value)
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