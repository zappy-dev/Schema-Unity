using System;
using Newtonsoft.Json;
using Schema.Core.IO;

namespace Schema.Core.Data
{
    [Serializable]
    public class AttributeDefinition : Defaultable, ICloneable, IComparable<AttributeDefinition>
    {
        #region Constants
        
        public const int DefaultColumnWidth = 150;
        
        #endregion

        #region Fields and Properties
        [JsonIgnore]
        internal DataScheme _scheme;

        #region Serialized Data

        public string AttributeName { get; set; }
        public DataType DataType { get; set; }
        public bool IsIdentifier { get; set; } = false;
        public bool ShouldPublish { get; set; } = true;

        #region UI Properties

        public string AttributeToolTip { get; set; }
        
        public int ColumnWidth { get; set; } = DefaultColumnWidth;

        #endregion

        #endregion

        #endregion

        public override string ToString()
        {
            return $"Attribute: '{AttributeName}' ({DataType})";
        }

        public AttributeDefinition(DataScheme scheme, 
            string attributeName, DataType dataType, 
            string attributeToolTip = "",
            object defaultValue = null,
            bool isIdentifier = false,
            bool shouldPublish = true) : base(defaultValue)
        {
            _scheme = scheme;
            AttributeName = attributeName;
            DataType = dataType;
            AttributeToolTip = attributeToolTip;
            IsIdentifier = isIdentifier;
            ShouldPublish = shouldPublish;
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
                DataType = DataType.Clone() as DataType,
                ColumnWidth = ColumnWidth,
                IsIdentifier = IsIdentifier,
                ShouldPublish = ShouldPublish
            };
        }

        /// <summary>
        /// Copy data from another instance to this instance
        /// </summary>
        /// <param name="other">Other instance to copy data from</param>
        public void Copy(AttributeDefinition other)
        {
            AttributeName = other.AttributeName;
            AttributeToolTip = other.AttributeToolTip;
            DefaultValue = other.DefaultValue;
            DataType = other.DataType.Clone() as DataType;
            ColumnWidth = other.ColumnWidth;
            IsIdentifier = other.IsIdentifier;
            ShouldPublish = other.ShouldPublish;
        }

        public SchemaResult<ReferenceDataType> CreateReferenceType(SchemaContext ctx)
        {
            if (!IsIdentifier)
            {
                return Fail<ReferenceDataType>("Attribute is not a identifier");
            }
            
            if (!Schema.GetOwnerSchemeForAttribute(ctx, this).Try(out var ownerScheme, out var ownerErr))
            {
                return ownerErr.CastError<ReferenceDataType>();
            }

            var refDataType = new ReferenceDataType(ownerScheme.SchemeName, AttributeName);
            return Pass(refDataType);
        }

        #region Equality
        public int CompareTo(AttributeDefinition other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            var attributeNameComparison = string.Compare(AttributeName, other.AttributeName, StringComparison.Ordinal);
            if (attributeNameComparison != 0) return attributeNameComparison;
            var isIdentifierComparison = IsIdentifier.CompareTo(other.IsIdentifier);
            if (isIdentifierComparison != 0) return isIdentifierComparison;
            // var dataTypeComparison = DataType.CompareTo(other.DataType);
            // if (dataTypeComparison != 0) return dataTypeComparison;
            var attributeTypeComparison = AttributeToolTip.CompareTo(other.AttributeToolTip);
            if (attributeTypeComparison != 0) return attributeTypeComparison;
            var columnWidthComparison = ColumnWidth.CompareTo(other.ColumnWidth);
            if (columnWidthComparison != 0) return columnWidthComparison;
            // var columnWidthComparison = DefaultValue.CompareTo(other.DefaultValue);
            // if (columnWidthComparison != 0) return columnWidthComparison;
            return ShouldPublish.CompareTo(other.ShouldPublish);
        }
        
        // Equality members should be focused on ensuring that data scheme related fields are the same
        // Less worried about comparing UI properties.
        protected bool Equals(AttributeDefinition other)
        {
            if (AttributeName != other.AttributeName) return false;
            if (!Equals(DataType, other.DataType)) return false;
            if (!Equals(IsIdentifier, other.IsIdentifier)) return false;
            if (!Equals(ShouldPublish, other.ShouldPublish)) return false;
            if (!Equals(AttributeToolTip, other.AttributeToolTip)) return false;
            if (!Equals(ColumnWidth, other.ColumnWidth)) return false;

            // special case equality for paths
            var defaultValue = DefaultValue;
            var otherDefaultValue = other.DefaultValue;
            if (DataType is FSDataType && defaultValue is string dv && otherDefaultValue is string odv)
            {
                defaultValue = PathUtility.SanitizePath(dv);
                otherDefaultValue = PathUtility.SanitizePath(odv);
            }
            
            if (!Equals(defaultValue, otherDefaultValue)) return false;
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

        public SchemaResult CheckIfValidData(SchemaContext context, object value)
        {
            using var _ = new AttributeContextScope(ref context, this.AttributeName);
            return DataType.IsValidValue(context, value);
        }

        public SchemaResult<object> ConvertData(SchemaContext context, object value)
        {
            return DataType.ConvertValue(context, value);
        }
    }
}