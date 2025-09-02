using System;
using System.Collections.Generic;
using System.Linq;

namespace Schema.Core.Data
{
    public partial class DataScheme
    {
        #region Attribute Operations

        #region Attribute Mutations

        public SchemaResult<AttributeDefinition> AddAttribute( 
            string attributeName, DataType dataType, 
            string attributeToolTip = null,
            object defaultValue = null,
            bool isIdentifier = false)
        {
            var newAttribute = new AttributeDefinition(this, attributeName, dataType, attributeToolTip, defaultValue,
                isIdentifier);

            var result = AddAttribute(newAttribute);
            
            return CheckIf(result.Passed, newAttribute, result.Message, "Created new attribute", Context);
        }
        
        public SchemaResult AddAttribute(AttributeDefinition newAttribute)
        {
            // Validate
            if (newAttribute == null)
            {
                return SchemaResult.Fail("Attribute cannot be null", this);
            }

            // Attribute naming validation
            string newAttributeName = newAttribute.AttributeName;
            if (string.IsNullOrWhiteSpace(newAttributeName))
            {
                return SchemaResult.Fail("Attribute name cannot be null or empty.", this);
            }
            
            if (attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResult.Fail("Duplicate attribute name: " + newAttributeName, this);
            }

            if (newAttribute.DataType == null)
            {
                return SchemaResult.Fail("Attribute data type cannot be null.", this);
            }
            
            // Commit
            newAttribute._scheme = this;
            attributes.Add(newAttribute);
            IsDirty = true;

            foreach (var entry in entries)
            {
                entry.SetData(newAttributeName, newAttribute.CloneDefaultValue());
            }
            
            return SchemaResult.Pass($"Added attribute {newAttributeName}", this);
        }

        public SchemaResult ConvertAttributeType(string attributeName, DataType newType)
        {
            var attribute = attributes.Find(a => a.AttributeName == attributeName);

            foreach (var entry in entries)
            {
                object convertedData;
                if (!entry.HasData(attribute))
                {
                    convertedData = newType.CloneDefaultValue();
                }
                else
                {
                    var entryData = entry.GetData(attribute).Result;

                    if (!DataType.ConvertData(entryData, attribute.DataType, newType, attribute.Context).Try(out convertedData))
                    {
                        return SchemaResult.Fail($"Cannot convert attribute {attributeName} to type {newType}", this);
                    }
                }
                        
                entry.SetData(attributeName, convertedData);
            }
            
            attribute.DataType = newType;
            // TODO: Does the abstract that an attribute can defined a separate default value than a type help right now?
            attribute.DefaultValue = newType.CloneDefaultValue();
            IsDirty = true;
            
            return SchemaResult.Pass($"Converted attribute {attributeName} to type {newType}", this);
        }

        public SchemaResult DeleteAttribute(AttributeDefinition attribute)
        {
            bool result = attributes.Remove(attribute);
            IsDirty = true;

            return CheckIf(result, errorMessage: $"Attribute {attribute} cannot be deleted",
                successMessage: $"Deleted {attribute}");
        }

        /// <summary>
        /// Changes an attribute name to a new name.
        /// </summary>
        /// <param name="prevAttributeName"></param>
        /// <param name="newAttributeName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public SchemaResult UpdateAttributeName(string prevAttributeName, string newAttributeName)
        {
            if (string.IsNullOrWhiteSpace(prevAttributeName) || string.IsNullOrWhiteSpace(newAttributeName))
            {
                return SchemaResult.Fail("Attribute name cannot be null or empty.", this);
            }
            
            if (prevAttributeName.Equals(newAttributeName))
            {
                return SchemaResult.Fail("Attribute name cannot be the same as previous attribute name.", this);
            }

            if (attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResult.Fail("Attribute name already exists.", this);
            }
            
            // for reference type fields, we gotta update anything that references this field.
            if (!GetAttribute(prevAttributeName).Try(out var prevAttribute))
            {
                return SchemaResult.Fail($"Attribute {prevAttributeName} cannot be found", this);
            }
            
            // update attribute and migrate entries
            prevAttribute.UpdateAttributeName(newAttributeName);
            foreach (var entry in entries)
            {
                entry.MigrateData(prevAttributeName, newAttributeName);
            }
            IsDirty = true;

            // update all referencing attributes in other Schemes
            foreach (var otherScheme in Schema.GetSchemes())
            {
                // skip checking my own schema.
                // TODO: Handle cyclical references from an attribute in a scheme to itself?
                if (otherScheme.SchemeName == SchemeName)
                {
                    continue;
                }

                // find attributes referencing the previous attribute name
                var referencingAttributes = otherScheme.GetAttributes(attr =>
                    {
                        if (attr.DataType is ReferenceDataType refDataType)
                        {
                            return refDataType.ReferenceSchemeName == SchemeName &&
                                   refDataType.ReferenceAttributeName == prevAttributeName;
                        }

                        return false;
                    }).Select(attr => attr.DataType as ReferenceDataType)
                    .Where(refDataType => refDataType != null);

                // update attributes to reference new attribute name
                int referencingAttributesUpdated = 0;
                foreach (var refDataType in referencingAttributes)
                {
                    refDataType.ReferenceAttributeName = newAttributeName;
                    referencingAttributesUpdated++;
                }

                // Update all data entries as well
                int entriesUpdated = 0;
                foreach (var entry in otherScheme.entries)
                {
                    entry.MigrateData(prevAttributeName, newAttributeName);
                    entriesUpdated++;
                }

                otherScheme.IsDirty = referencingAttributesUpdated > 0 && entriesUpdated > 0;
            }
            
            return SchemaResult.Pass("Updated attribute: " + prevAttributeName + " to " + newAttributeName, this);
        }

        #endregion

        #region Attribute Retrieval Queries
        
        public AttributeDefinition GetAttribute(int attributeIndex)
        {
            return attributes[attributeIndex];
        }

        public SchemaResult<AttributeDefinition> GetAttributeByName(string attributeName)
        {
            var attribute = attributes.FirstOrDefault(a => a.AttributeName.Equals(attributeName));
            
            return CheckIf(attribute != null, attribute, 
                errorMessage: "Attribute does not exist",
                successMessage: $"Attribute with name '{attributeName}' exist");
        }

        public SchemaResult<AttributeDefinition> GetAttribute(Func<AttributeDefinition, bool> predicate)
        {
            var attribute = attributes.FirstOrDefault(predicate);
            
            return CheckIf(attribute != null, attribute, errorMessage: "Attribute not found", successMessage: "Attribute found");
        }

        public IEnumerable<AttributeDefinition> GetAttributes(Func<AttributeDefinition, bool> predicate)
        {
            var matchingAttributes = attributes.Where(predicate);
            
            return matchingAttributes;
        }

        public IEnumerable<AttributeDefinition> GetAttributes()
        {
            return attributes;
        }

        public SchemaResult<AttributeDefinition> GetAttribute(string attributeName)
        {
            return GetAttribute(a => a.AttributeName == attributeName);
        }

        public SchemaResult<AttributeDefinition> GetIdentifierAttribute()
            => GetAttribute(a => a.IsIdentifier);
        
        public IEnumerable<AttributeDefinition> GetReferenceAttributes()
        {
            return attributes.Where(attr => attr.DataType is ReferenceDataType);
        }
        
        #endregion
        
        #region Attribute Ordering Operations

        public SchemaResult MoveAttributeRank(AttributeDefinition attribute, int moveRank)
        {
            return Move(attribute, moveRank, attributes);
        }
        
        public SchemaResult IncreaseAttributeRank(AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            var newIdx = attributeIdx - 1; // shift lower to appear sooner
            return Swap(attributeIdx, newIdx, attributes);
        }

        public SchemaResult DecreaseAttributeRank(AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            var newIdx = attributeIdx + 1; // shift higher to appear later
            return Swap(attributeIdx, newIdx, attributes);
        }

        #endregion
        
        #endregion
    }
}