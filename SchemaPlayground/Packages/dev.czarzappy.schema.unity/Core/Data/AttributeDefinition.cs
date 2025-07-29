using System;
using Newtonsoft.Json;

namespace Schema.Core.Data
{
    [Serializable]
    public class AttributeDefinition : Defaultable, ICloneable
    {
        protected override string Context => nameof(AttributeDefinition);
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
            DefaultValue = other.DefaultValue;
            DataType = other.DataType;
            ColumnWidth = other.ColumnWidth;
            IsIdentifier = other.IsIdentifier;
        }

        public SchemaResult<ReferenceDataType> CreateReferenceType()
        {
            if (!IsIdentifier)
            {
                return Fail<ReferenceDataType>("Attribute is not a identifier");
            }
            
            if (!Schema.TryGetSchemeForAttribute(this, out var ownerScheme))
            {
                return Fail<ReferenceDataType>($"{this} does not existing in any known scheme.");
            }

            var refDataType = new ReferenceDataType(ownerScheme.SchemeName, AttributeName);
            return Pass(refDataType);
        }

        #region Equality
        
        // Equality members should be focused on ensuring that data scheme related fields are the same
        // Less worried about comparing UI properties.
        protected bool Equals(AttributeDefinition other)
        {
            if (AttributeName != other.AttributeName) return false;
            if (!Equals(DataType, other.DataType)) return false;
            if (!Equals(IsIdentifier, other.IsIdentifier)) return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AttributeDefinition)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (AttributeName != null ? AttributeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (DataType != null ? DataType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsIdentifier.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(AttributeDefinition left, AttributeDefinition right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AttributeDefinition left, AttributeDefinition right)
        {
            return !Equals(left, right);
        }

        #endregion
        
        internal void UpdateAttributeName(string newAttributeName)
        {
            AttributeName = newAttributeName;
        }
    }
}