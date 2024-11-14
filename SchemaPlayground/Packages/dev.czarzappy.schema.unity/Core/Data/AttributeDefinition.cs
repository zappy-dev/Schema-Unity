using System;

namespace Schema.Core.Data
{
    [Serializable]
    public class AttributeDefinition : Defaultable, ICloneable
    {
        public const int DefaultColumnWidth = 150;
        public string AttributeName { get; set; }
        public string AttributeToolTip { get; set; }
        public DataType DataType { get; set; }
        public int ColumnWidth { get; set; } = DefaultColumnWidth;

        public bool IsIdentifier { get; set; } = false;

        public override string ToString()
        {
            return $"Attribute: '{AttributeName}' ({DataType})";
        }

        public AttributeDefinition(string attributeName, DataType dataType, 
            string attributeToolTip = null,
            object defaultValue = null,
            bool isIdentifier = false) : base(defaultValue)
        {
            AttributeName = attributeName;
            DataType = dataType;
            AttributeToolTip = attributeToolTip;
            IsIdentifier = isIdentifier;
        }
        
        public AttributeDefinition()
        {
            
        }

        public object Clone()
        {
            return new AttributeDefinition
            {
                AttributeName = AttributeName?.Clone() as string,
                AttributeToolTip = AttributeToolTip?.Clone() as string,
                DefaultValue = CloneDefaultValue(),
                DataType = DataType,
                ColumnWidth = ColumnWidth,
                IsIdentifier = IsIdentifier,  
            };
        }

        public void Copy(AttributeDefinition other)
        {
            AttributeName = other.AttributeName;
            AttributeToolTip = other.AttributeToolTip;
            DataType = other.DataType;
            ColumnWidth = other.ColumnWidth;
            IsIdentifier = other.IsIdentifier;
        }
    }
}