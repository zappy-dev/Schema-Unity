using System;

namespace Schema.Core
{
    [System.Serializable]
    public class AttributeDefinition : Defaultable
    {
        public const int DefaultColumnWidth = 150;
        public string AttributeName { get; set; }
        public string AttributeToolTip { get; set; }
        public DataType DataType { get; set; }
        public int ColumnWidth { get; set; } = DefaultColumnWidth;
    }
}