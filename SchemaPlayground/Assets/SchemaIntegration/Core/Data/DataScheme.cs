using System;
using System.Collections.Generic;

namespace Schema.Core
{
    // Representing a data scheme in memory
    public class DataScheme
    {
        public string SchemeName { get; set; }
        public List<AttributeDefinition> Attributes { get; set; }
        public List<DataEntry> Entries { get; set; }

        public DataScheme(string name)
        {
            SchemeName = name;
            Attributes = new List<AttributeDefinition>();
            Entries = new List<DataEntry>();
        }

        // Methods for adding/updating entries, etc.

        public override string ToString()
        {
            return $"DataSchema: {SchemeName}";
        }

        public void CreateNewEntry()
        {
            var entry = new DataEntry();
            foreach (var attribute in Attributes)
            {
                entry.EntryData[attribute.AttributeName] = attribute.DefaultValue;
            }
            
            Entries.Add(entry);
        }

        public SchemaResponse AddAttribute(string newAttributeName, DataType dataType, object defaultValue)
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
                DefaultValue = defaultValue
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
                            entryData = newType.DefaultValue;
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
            attribute.DefaultValue = newType.DefaultValue;
            
            return SchemaResponse.Success($"Successfully converted attribute {attributeName} to type {newType}");
        }
    }
}