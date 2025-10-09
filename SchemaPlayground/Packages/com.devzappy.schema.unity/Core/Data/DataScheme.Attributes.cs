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
            SchemaContext context,
            string attributeName, DataType dataType, 
            string attributeToolTip = "",
            object defaultValue = null,
            bool isIdentifier = false,
            bool shouldPublish = true)
        {
            var newAttribute = new AttributeDefinition(this, attributeName, dataType, attributeToolTip, defaultValue,
                isIdentifier, shouldPublish);

            var result = AddAttribute(context, newAttribute);
            
            return CheckIf(result.Passed, newAttribute, result.Message, "Created new attribute", context);
        }
        
        public SchemaResult AddAttribute(SchemaContext context, AttributeDefinition newAttribute)
        {
            // Validate
            if (newAttribute == null)
            {
                return SchemaResult.Fail(context, "Attribute cannot be null");
            }

            // Attribute naming validation
            string newAttributeName = newAttribute.AttributeName;
            if (string.IsNullOrWhiteSpace(newAttributeName))
            {
                return SchemaResult.Fail(context, "Attribute name cannot be null or empty.");
            }
            
            if (attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResult.Fail(context, "Duplicate attribute name: " + newAttributeName);
            }

            if (newAttribute.DataType == null)
            {
                return SchemaResult.Fail(context, "Attribute data type cannot be null.");
            }
            
            // Ensure attributes always carry a concrete default value for population of entries
            if (newAttribute.DefaultValue == null)
            {
                newAttribute.DefaultValue = newAttribute.DataType.CloneDefaultValue();
            }
            
            // Commit
            newAttribute._scheme = this;
            attributes.Add(newAttribute);
            SetDirty(context, true);

            foreach (var entry in entries)
            {
                entry.SetData(context, newAttributeName, newAttribute.CloneDefaultValue());
            }
            
            return SchemaResult.Pass($"Added attribute {newAttributeName}", context);
        }

        public SchemaResult ConvertAttributeType(SchemaContext context, string attributeName, DataType newType)
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

                    if (!DataType.ConvertValue(context, entryData, attribute.DataType, newType).Try(out convertedData, out var convertErr))
                    {
                        return SchemaResult.Fail(context, $"Cannot convert attribute {attributeName} to type {newType}, reason: {convertErr}");
                    }
                }
                        
                entry.SetData(context, attributeName, convertedData);
            }
            
            attribute.DataType = newType;
            // TODO: Does the abstract that an attribute can defined a separate default value than a type help right now?
            attribute.DefaultValue = newType.CloneDefaultValue();
            SetDirty(context, true);
            
            return SchemaResult.Pass($"Converted attribute {attributeName} to type {newType}", context);
        }

        public SchemaResult DeleteAttribute(SchemaContext context, AttributeDefinition attribute)
        {
            bool result = attributes.Remove(attribute);
            SetDirty(context, true);

            return CheckIf(result, errorMessage: $"Attribute {attribute} cannot be deleted",
                successMessage: $"Deleted {attribute}", context: context);
        }

        /// <summary>
        /// Changes an attribute name to a new name.
        /// </summary>
        /// <param name="prevAttributeName"></param>
        /// <param name="newAttributeName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public SchemaResult UpdateAttributeName(SchemaContext context, string prevAttributeName, string newAttributeName)
        {
            if (string.IsNullOrWhiteSpace(prevAttributeName) || string.IsNullOrWhiteSpace(newAttributeName))
            {
                return SchemaResult.Fail(context, "Attribute name cannot be null or empty.");
            }
            
            if (prevAttributeName.Equals(newAttributeName))
            {
                return SchemaResult.Fail(context, "Attribute name cannot be the same as previous attribute name.");
            }

            if (attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResult.Fail(context, "Attribute name already exists.");
            }
            
            // for reference type fields, we gotta update anything that references this field.
            if (!GetAttribute(prevAttributeName).Try(out var prevAttribute))
            {
                return SchemaResult.Fail(context, $"Attribute {prevAttributeName} cannot be found");
            }
            
            // update attribute and migrate entries
            prevAttribute.UpdateAttributeName(newAttributeName);
            foreach (var entry in entries)
            {
                entry.MigrateData(context, prevAttributeName, newAttributeName);
            }
            SetDirty(context, true);

            // update all referencing attributes in other Schemes
            foreach (var otherScheme in Schema.GetSchemes(context))
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
                    entry.MigrateData(context, prevAttributeName, newAttributeName);
                    entriesUpdated++;
                }

                if (referencingAttributesUpdated > 0 && entriesUpdated > 0)
                {
                    otherScheme.SetDirty(context, true);
                }
            }
            
            return SchemaResult.Pass("Updated attribute: " + prevAttributeName + " to " + newAttributeName, context);
        }

        #endregion

        #region Attribute Retrieval Queries
        
        public SchemaResult<AttributeDefinition> GetAttribute(int attributeIndex, SchemaContext context = default)
        {
            var res = SchemaResult<AttributeDefinition>.New(context);
            if (attributeIndex < 0 || attributeIndex >= this.AttributeCount)
            {
                return res.Fail($"Attribute Index ({attributeIndex}) is out of range [0,{this.AttributeCount})");
            }

            return res.Pass(attributes[attributeIndex]);
        }

        public SchemaResult<AttributeDefinition> GetAttributeByName(string attributeName, SchemaContext context = default)
        {
            var attribute = attributes.FirstOrDefault(a => a.AttributeName.Equals(attributeName));
            
            return CheckIf(attribute != null, attribute, 
                errorMessage: $"Attribute '{attributeName}' does not exist",
                successMessage: $"Attribute with name '{attributeName}' exist", context: context);
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

        public SchemaResult MoveAttributeRank(SchemaContext context, AttributeDefinition attribute, int moveRank)
        {
            return Move(context, attribute, moveRank, attributes);
        }
        
        public SchemaResult IncreaseAttributeRank(SchemaContext context, AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            var newIdx = attributeIdx - 1; // shift lower to appear sooner
            return Swap(context, attributeIdx, newIdx, attributes);
        }

        public SchemaResult DecreaseAttributeRank(SchemaContext context, AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            var newIdx = attributeIdx + 1; // shift higher to appear later
            return Swap(context, attributeIdx, newIdx, attributes);
        }

        #endregion
        
        #endregion
    }
}