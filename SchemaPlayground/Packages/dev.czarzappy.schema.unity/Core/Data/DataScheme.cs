using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Schema.Core.Data
{
    // Representing a data scheme in memory
    [Serializable]
    public class DataScheme
    {
        #region Fields
        
        [JsonProperty("SchemeName")]
        public string SchemeName { get; set; }
        
        [JsonProperty("Attributes")]
        private List<AttributeDefinition> attributes { get; set; }
        
        [JsonIgnore]

        private IEnumerable<AttributeDefinition> AllAttributes => attributes;
        
        [JsonProperty("Entries")]
        private List<DataEntry> entries { get; set; }
        
        [JsonIgnore]
        public IEnumerable<DataEntry> AllEntries => entries;
        
        [JsonIgnore]
        public int EntryCount => entries?.Count ?? 0;
        
        [JsonIgnore]
        public int AttributeCount => attributes?.Count ?? 0;
        
        [JsonIgnore]
        public bool IsManifest => Schema.MANIFEST_SCHEME_NAME.Equals(SchemeName);
        
        [JsonIgnore]
        public bool HasIdentifierAttribute
            => TryGetAttribute(a => a.IsIdentifier, out _);

        #endregion

        public DataScheme()
        {
            attributes = new List<AttributeDefinition>();
            entries = new List<DataEntry>();
        }

        public DataScheme(string schemeName, List<AttributeDefinition> attributes, List<DataEntry> entries)
        {
            SchemeName = schemeName;
            this.attributes = attributes;
            this.entries = entries;
        }

        public DataScheme(string schemeName) : this(schemeName, new List<AttributeDefinition>(), new List<DataEntry>())
        {
            SchemeName = schemeName;
        }

        // Methods for adding/updating entries, etc.

        public override string ToString()
        {
            return $"DataScheme '{SchemeName}'";
        }

        #region Attribute Operations
        public SchemaResult AddAttribute(AttributeDefinition newAttribute)
        {
            if (newAttribute == null)
            {
                return SchemaResult.Fail("Attribute cannot be null", this);
            }

            string newAttributeName = newAttribute.AttributeName;
            if (string.IsNullOrEmpty(newAttributeName))
            {
                return SchemaResult.Fail("Attribute name cannot be null or empty.", this);
            }
            
            if (attributes.Exists(a => a.AttributeName == newAttributeName))
            {
                return SchemaResult.Fail("Duplicate attribute name: " + newAttributeName, this);
            }
            
            attributes.Add(newAttribute);

            foreach (var entry in entries)
            {
                entry.SetData(newAttributeName, newAttribute.CloneDefaultValue());
            }
            
            return SchemaResult.Success($"Added attribute {newAttributeName} to all entries", this);
        }

        public SchemaResult ConvertAttributeType(string attributeName, DataType newType)
        {
            var attribute = attributes.Find(a => a.AttributeName == attributeName);

            try
            {
                foreach (var entry in entries)
                {
                    object convertedData;
                    if (!entry.HasData(attribute))
                    {
                        convertedData = newType.CloneDefaultValue();
                    }
                    else
                    {
                        var entryData = entry.GetData(attribute);

                        if (!DataType.TryToConvertData(entryData, attribute.DataType, newType, out convertedData))
                        {
                            return SchemaResult.Fail($"Cannot convert attribute {attributeName} to type {newType}", this);
                        }
                    }
                        
                    entry.SetData(attributeName, convertedData);
                }
            }
            catch (FormatException e)
            {
                return SchemaResult.Fail("Failed to convert attribute " + attributeName + " to type " + newType + ": " + e.Message, this);
            }
            
            attribute.DataType = newType;
            // TODO: Does the abstract that an attribute can defined a separate default value than a type help right now?
            attribute.DefaultValue = newType.CloneDefaultValue();
            
            return SchemaResult.Success($"Converted attribute {attributeName} to type {newType}", this);
        }

        public SchemaResult DeleteAttribute(AttributeDefinition attribute)
        {
            bool result = attributes.Remove(attribute);
            if (result == false)
            {
                return SchemaResult.Fail($"Attribute {attribute.AttributeName} cannot be deleted", this);
            }
            else
            {
                return SchemaResult.Success($"Deleted {attribute}", this);
            }
        }

        /// <summary>
        /// Changes an attribute name to a new name.
        /// </summary>
        /// <param name="prevAttributeName"></param>
        /// <param name="newAttributeName"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public SchemaResult UpdateAttributeName(string prevAttributeName, string newAttributeName)
        {
            if (string.IsNullOrEmpty(prevAttributeName) || string.IsNullOrEmpty(newAttributeName))
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
            
            attributes.Find(a => a.AttributeName == prevAttributeName).AttributeName = newAttributeName;
            foreach (var entry in entries)
            {
                entry.MigrateData(prevAttributeName, newAttributeName);
            }
            
            return SchemaResult.Success("Updated attribute: " + prevAttributeName + " to " + newAttributeName, this);
        }

        public bool TryGetAttribute(Func<AttributeDefinition, bool> predicate, out AttributeDefinition attribute)
        {
            attribute = attributes.FirstOrDefault(predicate);
            return attribute != null;;
        }

        public bool TryGetIdentifierAttribute(out AttributeDefinition identifierAttribute)
            => TryGetAttribute(a => a.IsIdentifier, out identifierAttribute);

        #region Attribute Ordering Operations
        
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

        #region Entry Operations
        
        public DataEntry CreateNewEntry()
        {
            var entry = new DataEntry();
            foreach (var attribute in attributes)
            {
                entry.SetData(attribute.AttributeName, attribute.CloneDefaultValue());
            }
            
            entries.Add(entry);
            return entry;
        }

        public SchemaResult AddEntry(DataEntry newEntry)
        {
            Logger.Log($"Adding {newEntry}...", this);
            if (newEntry is null)
            {
                return SchemaResult.Fail("Entry cannot be null", this);
            }
            
            // TODO: Validate that a data entry has all of the expected attributes and add default attribute values if not present
            // Also fail if unexpected attribute values are encountered? 
            
            entries.Add(newEntry);
            return SchemaResult.Success($"Added {newEntry}", this);
        }

        public SchemaResult DeleteEntry(DataEntry entry)
        {
            entries.Remove(entry);
            return SchemaResult.Success("Successfully deleted entry", this);
        }

        public DataEntry GetEntry(Func<DataEntry, bool> entryFilter)
        {
            return entries.FirstOrDefault(entryFilter);
        }

        public bool TryGetEntry(Func<DataEntry, bool> entryFilter, out DataEntry entry)
        {
            entry = entries.FirstOrDefault(entryFilter);
            return entry != null;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataEntry GetEntry(int entryIndex)
        {
            return entries[entryIndex];
        }

        #region Entry Ordering Operations
        
        public SchemaResult MoveUpEntry(DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            var newIdx = entryIdx - 1;
            return SwapEntries(entryIdx, newIdx);
        }

        public SchemaResult MoveDownEntry(DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            var newIdx = entryIdx + 1;
            return SwapEntries(entryIdx, newIdx);
        }

        public SchemaResult MoveEntry(DataEntry entry, int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= entries.Count)
            {
                return SchemaResult.Fail($"Target index {targetIndex} is out of range.", this);
            }
            
            var entryIdx = entries.IndexOf(entry);
            if (entryIdx == -1)
            {
                return SchemaResult.Fail("Entry not found", this);
            }
            if (entryIdx == targetIndex)
            {
                return SchemaResult.Fail("Entry cannot be the same as the target.", this);
            }
            entries.RemoveAt(entryIdx);
            entries.Insert(targetIndex, entry);
            
            return SchemaResult.Success($"Moved {entry} from {entryIdx} to {targetIndex}", this);
        }

        public SchemaResult SwapEntries(int srcIndex, int dstIndex)
        {
            return Swap(srcIndex, dstIndex, entries);
        }
        
        public SchemaResult Swap<T>(int srcIndex, int dstIndex, List<T> data)
        {
            if (srcIndex < 0 || srcIndex >= data.Count)
            {
                return SchemaResult.Fail($"Attempted to move entry from invalid index {srcIndex}.", this);
            }
            
            if (dstIndex < 0 || dstIndex >= data.Count)
            {
                return SchemaResult.Fail($"Attempted to move entry {srcIndex} to invalid destination {dstIndex} is out of range.", this);
            }
            
            (data[srcIndex], data[dstIndex]) = (data[dstIndex], data[srcIndex]);
            return SchemaResult.Success("Successfully swapped entry", this);
        }

        #endregion

        #endregion
        
        #region Value Operations
        
        public IEnumerable<object> GetIdentifierValues()
        {
            if (!TryGetIdentifierAttribute(out var identifierAttribute)) return Enumerable.Empty<object>();

            return GetValuesForAttribute(identifierAttribute).Distinct();;
        }

        public IEnumerable<object> GetValuesForAttribute(string attributeName)
        {
            if (string.IsNullOrEmpty(attributeName))
            {
                return Enumerable.Empty<object>();
            }

            
            if (!TryGetAttribute(a => a.AttributeName.Equals(attributeName), out var attribute))
            {
                return Enumerable.Empty<object>();
            }
            
            return GetValuesForAttribute(attribute);
        }

        public IEnumerable<object> GetValuesForAttribute(AttributeDefinition attribute)
        {
            if (attribute is null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            if (!attributes.Contains(attribute))
            {
                throw new InvalidOperationException(
                    $"Attempted to get attribute values for attribute not contained by Schema");
            }
            
            return entries.Select(e => e.GetData(attribute));
        }
        
        #endregion

        public AttributeDefinition GetAttribute(int attributeIndex)
        {
            return attributes[attributeIndex];
        }

        public bool TryGetAttribute(string attributeName, out AttributeDefinition attribute)
        {
            attribute = attributes.FirstOrDefault(a => a.AttributeName.Equals(attributeName));
            return attribute != null;
        }

        public IEnumerable<DataEntry> GetEntries(AttributeSortOrder sortOrder = default)
        {
            if (!sortOrder.HasValue)
            {
                return entries;
            }

            var attributeName = sortOrder.AttributeName;
            if (!TryGetAttribute(attributeName, out var attribute))
            {
                throw new ArgumentException($"Attempted to get entries for attribute not contained by Schema");
            }

            return sortOrder.Order == SortOrder.Ascending ? 
                entries.OrderBy(e => e.GetData(attribute)) : 
                entries.OrderByDescending(e => e.GetData(attribute));
        }
    }
}