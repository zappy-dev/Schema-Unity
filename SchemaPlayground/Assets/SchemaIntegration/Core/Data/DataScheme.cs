using System;
using System.Collections.Generic;
using System.Linq;

namespace Schema.Core
{
    // Representing a data scheme in memory
    [Serializable]
    public class DataScheme
    {
        #region Fields
        
        public string SchemaName { get; set; }
        public List<AttributeDefinition> Attributes { get; set; }
        public List<DataEntry> Entries { get; set; }

        #endregion

        public DataScheme()
        {
            Attributes = new List<AttributeDefinition>();
            Entries = new List<DataEntry>();
        }

        public DataScheme(string name) : this()
        {
            SchemaName = name;
        }

        // Methods for adding/updating entries, etc.

        public override string ToString()
        {
            return $"DataSchema {SchemaName}";
        }

        public void CreateNewEntry()
        {
            var entry = new DataEntry();
            foreach (var attribute in Attributes)
            {
                entry.SetData(attribute.AttributeName, attribute.CloneDefaultValue());
            }
            
            Entries.Add(entry);
        }

        public SchemaResponse AddAttribute(AttributeDefinition newAttribute)
        {
            if (newAttribute == null)
            {
                return SchemaResponse.Error("Attribute cannot be null");
            }

            string newAttributeName = newAttribute.AttributeName;
            if (string.IsNullOrEmpty(newAttributeName))
            {
                return SchemaResponse.Error("Attribute name cannot be null or empty.");
            }
            
            if (Attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResponse.Error("Duplicate attribute name: " + newAttributeName);
            }
            
            Attributes.Add(newAttribute);

            foreach (var entry in Entries)
            {
                entry.SetData(newAttributeName, newAttribute.CloneDefaultValue());
            }
            
            return SchemaResponse.Success("Successfully added attribute: " + newAttributeName);
        }

        public SchemaResponse ConvertAttributeType(string attributeName, DataType newType)
        {
            var attribute = Attributes.Find(a => a.AttributeName == attributeName);
            var prevDataType = attribute.DataType;
            
            try
            {
                foreach (var entry in Entries)
                {
                    var entryData = entry.GetData(attributeName);
                    if (!DataType.TryToConvertData(entryData, prevDataType, newType, out entryData))
                    {
                        return SchemaResponse.Error($"Cannot convert attribute {attributeName} to type {newType}");
                    }
                        
                    entry.SetData(attributeName, entryData);
                }
            }
            catch (FormatException e)
            {
                return SchemaResponse.Error("Failed to convert attribute " + attributeName + " to type " + newType + ": " + e.Message);
            }
            
            attribute.DataType = newType;
            // TODO: Does the abstract that an attribute can defined a separate default value than a type help right now?
            attribute.DefaultValue = newType.CloneDefaultValue();
            
            return SchemaResponse.Success($"Successfully converted attribute {attributeName} to type {newType}");
        }

        public void IncreaseAttributeRank(AttributeDefinition attribute)
        {
            var attributeIdx = Attributes.IndexOf(attribute);
            var newIdx = attributeIdx - 1; // shift lower to appear sooner
            Attributes[attributeIdx] = Attributes[newIdx];
            Attributes[newIdx] = attribute;
        }

        public void DecreaseAttributeRank(AttributeDefinition attribute)
        {
            var attributeIdx = Attributes.IndexOf(attribute);
            var newIdx = attributeIdx + 1; // shift higher to appear later
            Attributes[attributeIdx] = Attributes[newIdx];
            Attributes[newIdx] = attribute;
        }

        public void MoveUpEntry(DataEntry entry)
        {
            var entryIdx = Entries.IndexOf(entry);
            var newIdx = entryIdx - 1;
            SwapEntries(entryIdx, newIdx);
        }

        public void SwapEntries(int srcIndex, int dstIndex)
        {
            if (srcIndex < 0 || srcIndex >= Entries.Count)
            {
                Logger.LogError($"Attempted to move entry from invalid index {srcIndex}.");
                return;
            }
            
            if (dstIndex < 0 || dstIndex >= Entries.Count)
            {
                Logger.LogError($"Attempted to move entry {srcIndex} to invalid destination {dstIndex} is out of range.");
                return;
            }
            
            (Entries[srcIndex], Entries[dstIndex]) = (Entries[dstIndex], Entries[srcIndex]);
        }

        public void MoveDownEntry(DataEntry entry)
        {
            var entryIdx = Entries.IndexOf(entry);
            var newIdx = entryIdx + 1;
            SwapEntries(entryIdx, newIdx);
        }

        public void MoveEntry(DataEntry entry, int targetIndex)
        {
            var entryIdx = Entries.IndexOf(entry);
            Entries.RemoveAt(entryIdx);
            Entries.Insert(targetIndex, entry);
        }

        public SchemaResponse DeleteEntry(DataEntry entry)
        {
            Entries.Remove(entry);
            return SchemaResponse.Success("Successfully deleted entry");
        }

        public SchemaResponse DeleteAttribute(AttributeDefinition attribute)
        {
            Attributes.Remove(attribute);
            return SchemaResponse.Success("Successfully deleted attribute: " + attribute.AttributeName);
        }

        public void UpdateAttributeName(string prevAttributeName, string newAttributeName)
        {
            if (prevAttributeName.Equals(newAttributeName))
            {
                return;
            }

            if (Attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                // TODO: how to bubble this error?
                throw new InvalidOperationException($"Duplicate attribute name: {newAttributeName}");
            }
            
            Attributes.Find(a => a.AttributeName == prevAttributeName).AttributeName = newAttributeName;
            foreach (var entry in Entries)
            {
                entry.MigrateData(prevAttributeName, newAttributeName);
            }
        }

        public bool TryGetIdentifierAttribute(out AttributeDefinition identifierAttribute)
        {
            identifierAttribute = Attributes.FirstOrDefault(a => a.IsIdentifier);
            return identifierAttribute != null;;
        }

        public IEnumerable<object> GetIdentifierValues()
        {
            if (!TryGetIdentifierAttribute(out var identifierAttribute)) return Enumerable.Empty<object>();

            return GetAttributeValues(identifierAttribute);
        }

        public IEnumerable<object> GetAttributeValues(AttributeDefinition attribute)
        {
            if (attribute is null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            if (!Attributes.Contains(attribute))
            {
                throw new InvalidOperationException(
                    $"Attempted to get attribute values for attribute not contained by Schema");
            }
            
            return Entries.Select(e => e.GetData(attribute.AttributeName));
        }
    }
}