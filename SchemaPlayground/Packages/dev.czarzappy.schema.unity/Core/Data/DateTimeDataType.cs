using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class DateTimeDataType : DataType
    {
        public override string TypeName => "Date Time";
        public override bool IsValid(object value)
        {
            return value is DateTime;
        }

        public DateTimeDataType() : base(System.DateTime.UtcNow)
        {
            
        }

        public DateTimeDataType(DateTime dateTime) : base(dateTime)
        {
            
        }
    }
}