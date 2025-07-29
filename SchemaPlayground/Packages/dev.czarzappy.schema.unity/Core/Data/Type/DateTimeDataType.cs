using System;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    [Serializable]
    public class DateTimeDataType : DataType
    {
        public override string TypeName => "Date Time";
        public override SchemaResult CheckIfValidData(object value)
        {
            return CheckIf(value is DateTime, 
                errorMessage: "Value is not a DateTime",
                successMessage: "Value is a DateTime");

        }

        public override SchemaResult<object> ConvertData(object value)
        {
            var data = value as string;
            
            bool result = System.DateTime.TryParse(data, out var date);
            return SchemaResult<object>.CheckIf(result, date,
                errorMessage: "Unable to convert value",
                successMessage: "Converted value into DateTime", 
                context: this);
        }

        public DateTimeDataType() : base(System.DateTime.Today)
        {
            
        }

        public DateTimeDataType(DateTime dateTime) : base(dateTime)
        {
            
        }

        protected override string Context => nameof(DateTimeDataType);
    }
}