using System;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    [Serializable]
    public class DateTimeDataType : DataType
    {
        public override string TypeName => "Date Time";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => SchemaResult<string>.Pass($"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsDateTime)}(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(DateTime).ToString();
        public override object Clone()
        {
            return new DateTimeDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            return CheckIf(value is DateTime, 
                errorMessage: "Value is not a DateTime",
                successMessage: "Value is a DateTime", context);

        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            var data = value as string;
            
            bool result = System.DateTime.TryParse(data, out var date);
            date = date.ToUniversalTime(); // Always try to convert a date to UTC for consistency across devices
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