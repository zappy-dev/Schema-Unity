using System;
using System.Collections.Generic;

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
            return $"DataSchema: {SchemaName}";
        }

        public void CreateNewEntry()
        {
            var entry = new DataEntry();
            foreach (var attribute in Attributes)
            {
                entry.EntryData[attribute.AttributeName] = attribute.CloneDefaultValue();
            }
            
            Entries.Add(entry);
        }

        public SchemaResponse AddAttribute(string newAttributeName, DataType dataType, ICloneable defaultValue)
        {
            if (string.IsNullOrEmpty(newAttributeName))
            {
                return SchemaResponse.Error("Attribute name cannot be null or empty.");
            }
            
            if (Attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResponse.Error("Duplicate attribute name: " + newAttributeName);
            }
            
            Attributes.Add(new AttributeDefinition
            {
                AttributeName = newAttributeName,
                DataType = dataType,
                DefaultValue = defaultValue,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
            });

            foreach (var entry in Entries)
            {
                entry.EntryData[newAttributeName] = defaultValue;
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
                    var entryData = entry.EntryData[attributeName];

                    if (prevDataType.Equals(DataType.String))
                    {
                        string data = (string)entryData;
                        if (string.IsNullOrEmpty(data))
                        {
                            entryData = newType.CloneDefaultValue();
                        }
                        else if (newType.Equals(DataType.Integer))
                        {
                            entryData = Convert.ToInt32(data);
                        }
                        else if (newType.Equals(DataType.DateTime))
                        {
                            entryData = DateTime.Parse(data);
                        }
                    }
                    else if (newType.Equals(DataType.String))
                    {
                        entryData = entryData.ToString();
                    }
                    else
                    {
                        return SchemaResponse.Error($"Cannot convert attribute {attributeName} to type {newType}");
                    }
                        
                    entry.EntryData[attributeName] = entryData;
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
            Entries[entryIdx] = Entries[newIdx];
            Entries[newIdx] = entry;
        }

        public void MoveDownEntry(DataEntry entry)
        {
            var entryIdx = Entries.IndexOf(entry);
            var newIdx = entryIdx + 1;
            Entries[entryIdx] = Entries[newIdx];
            Entries[newIdx] = entry;
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
                entry.EntryData[newAttributeName] = entry.EntryData[prevAttributeName];
                entry.EntryData.Remove(prevAttributeName);
            }
        }
    }
}