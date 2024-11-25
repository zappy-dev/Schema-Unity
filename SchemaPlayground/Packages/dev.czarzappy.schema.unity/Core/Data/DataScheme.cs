using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Schema.Core.Ext;

namespace Schema.Core.Data
{
    // Representing a data scheme in memory
    [Serializable]
    public class DataScheme : ResultGenerator
    {
        #region Fields
        
        [JsonProperty("SchemeName")]
        public string SchemeName { get; set; }
        
        [JsonProperty("Attributes")]
        private List<AttributeDefinition> attributes { get; set; }
        
        [JsonIgnore]

        private IEnumerable<AttributeDefinition> AllAttributes => attributes;
        
        [JsonProperty("Entries")]
        private LinkedList<DataEntry> entries { get; set; }
        
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
            => GetAttribute(a => a.IsIdentifier).Passed;

        #endregion

        public DataScheme()
        {
            attributes = new List<AttributeDefinition>();
            entries = new LinkedList<DataEntry>();
        }

        public DataScheme(string schemeName, List<AttributeDefinition> attributes, LinkedList<DataEntry> entries)
        {
            SchemeName = schemeName;
            this.attributes = attributes;
            this.entries = entries;
        }

        public DataScheme(string schemeName) : this(schemeName, new List<AttributeDefinition>(), new LinkedList<DataEntry>())
        {
            SchemeName = schemeName;
        }

        // Methods for adding/updating entries, etc.

        public override string ToString()
        {
            return ToString(true);
        }

        public string ToString(bool verbose)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"DataScheme '{SchemeName}', {AttributeCount} attributes, {AllEntries.Count()} entries");
            if (verbose)
            {
                stringBuilder.AppendLine("==== Attributes ====");
                foreach (var attribute in attributes)
                {
                    stringBuilder.AppendLine($"\t{attribute}");
                }
                stringBuilder.AppendLine("==== Entries ====");
                foreach (var entry in entries)
                {
                    stringBuilder.AppendLine($"\t{entry}");
                }
            }
            return stringBuilder.ToString();
        }

        #region Attribute Operations
        public SchemaResult AddAttribute(AttributeDefinition newAttribute)
        {
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
            
            attributes.Add(newAttribute);

            foreach (var entry in entries)
            {
                entry.SetData(newAttributeName, newAttribute.CloneDefaultValue());
            }
            
            return SchemaResult.Pass($"Added attribute {newAttributeName} to all entries", this);
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

                    if (!DataType.ConvertData(entryData, attribute.DataType, newType).Try(out convertedData))
                    {
                        return SchemaResult.Fail($"Cannot convert attribute {attributeName} to type {newType}", this);
                    }
                }
                        
                entry.SetData(attributeName, convertedData);
            }
            
            attribute.DataType = newType;
            // TODO: Does the abstract that an attribute can defined a separate default value than a type help right now?
            attribute.DefaultValue = newType.CloneDefaultValue();
            
            return SchemaResult.Pass($"Converted attribute {attributeName} to type {newType}", this);
        }

        public SchemaResult DeleteAttribute(AttributeDefinition attribute)
        {
            bool result = attributes.Remove(attribute);

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

            // update all referencing attributes
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
                foreach (var refDataType in referencingAttributes)
                {
                    refDataType.ReferenceAttributeName = newAttributeName;
                }
            }
            
            return SchemaResult.Pass("Updated attribute: " + prevAttributeName + " to " + newAttributeName, this);
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

        #region Attribute Ordering Operations
        
        public SchemaResult MoveAttributeForward(AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            var newIdx = attributeIdx - 1; // shift lower to appear sooner
            return Swap(attributeIdx, newIdx, attributes);
        }

        public SchemaResult MoveAttributeBack(AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            var newIdx = attributeIdx + 1; // shift higher to appear later
            return Swap(attributeIdx, newIdx, attributes);
        }
        
        public SchemaResult MoveAttributeToFront(AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            return Swap(attributeIdx, 0, attributes);
        }

        public SchemaResult MoveAttributeToBack(AttributeDefinition attribute)
        {
            var attributeIdx = attributes.IndexOf(attribute);
            return Swap(attributeIdx, attributes.Count - 1, attributes);
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
            
            entries.AddLast(entry);
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

            foreach (var kvp in newEntry)
            {
                string attributeName = kvp.Key;
                // Don't need to validate invalid attribute names, since adding new entry data already does that.

                if (!GetAttributeByName(attributeName).Try(out var attribute))
                {
                    return SchemaResult.Fail($"No matching attribute found for '{kvp.Key}'", this);
                }

                var entryValue = kvp.Value;
                var isValidRes = attribute.DataType.CheckIfValidData(entryValue);
                if (isValidRes.Failed)
                {
                    return isValidRes;
                }
            }

            foreach (var attribute in AllAttributes)
            {
                if (newEntry.HasData(attribute))
                {
                    continue;
                }

                newEntry.SetData(attribute.AttributeName, attribute.CloneDefaultValue());
            }
            
            entries.AddLast(newEntry);
            return SchemaResult.Pass($"Added {newEntry}", this);
        }

        public SchemaResult DeleteEntry(DataEntry entry)
        {
            bool result = entries.Remove(entry);
            return SchemaResult.CheckIf(result, 
                errorMessage: "Could not delete entry",
                successMessage: "Removed entry");
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
            return entries.ElementAt(entryIndex);
        }

        #region Entry Ordering Operations
        
        public SchemaResult MoveUpEntry(DataEntry entry)
        {
            return MoveUp(entries, entry);
        }

        private SchemaResult MoveUp<T>(LinkedList<T> list, T entry)
        {
            var entryNode = list.Find(entry);
            if (entryNode == null)
            {
                return Fail("Entry not found");
            }
            
            if (entryNode.Previous == null)
            {
                return Fail("Entry already moved to the top");
            }

            list.Remove(entryNode);
            list.AddBefore(entryNode, entryNode.Previous);
            return Pass("Entry moved up");
        }

        public SchemaResult MoveDownEntry(DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            var newIdx = entryIdx + 1;
            return SwapEntries(entryIdx, newIdx);
        }
        
        public SchemaResult MoveEntryToTop(DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            return SwapEntries(entryIdx, 0);
        }

        public SchemaResult MoveEntryToBottom(DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            return SwapEntries(entryIdx, entries.Count - 1);
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

            if (srcIndex == dstIndex)
            {
                return SchemaResult.Pass($"Entry already in target position", this);
            }
            
            (data[srcIndex], data[dstIndex]) = (data[dstIndex], data[srcIndex]);
            return SchemaResult.Pass("Successfully swapped entry", this);
        }

        #endregion

        #endregion
        
        #region Value Operations
        
        public IEnumerable<object> GetIdentifierValues()
        {
            var identifierAttrRes = GetIdentifierAttribute();
            if (identifierAttrRes.Failed)
            {
                return Enumerable.Empty<object>();
            }

            return GetValuesForAttribute(identifierAttrRes.Result).Distinct();;
        }

        public IEnumerable<object> GetValuesForAttribute(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                return Enumerable.Empty<object>();
            }
            
            if (!GetAttribute(a => a.AttributeName.Equals(attributeName)).Try(out var attribute))
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
            
            return entries.Select(e => e.GetData(attribute))
                .Where(r => r.Passed)
                .Select(r => r.Result);
        }
        
        #endregion

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

        public IEnumerable<DataEntry> GetEntries(AttributeSortOrder sortOrder = default)
        {
            if (!sortOrder.HasValue)
            {
                return entries;
            }

            var attributeName = sortOrder.AttributeName;
            if (!GetAttributeByName(attributeName).Try(out var attribute))
            {
                throw new ArgumentException($"Attempted to get entries using invalid sort attribute: {attributeName}");
            }

            return sortOrder.Order == SortOrder.Ascending ? 
                entries.OrderBy(e => e.GetData(attribute).Result) : 
                entries.OrderByDescending(e => e.GetData(attribute).Result);
        }

        public SchemaResult Load(bool overwriteExisting = false)
        {
            return Schema.LoadDataScheme(this, overwriteExisting: overwriteExisting);
        }

        public IEnumerable<AttributeDefinition> GetReferenceAttributes()
        {
            return attributes.Where(attr => attr.DataType is ReferenceDataType);
        }

        #region Equality
        
        protected bool Equals(DataScheme other)
        {
            if (SchemeName != other.SchemeName) return false;
            if (!ListExt.ListsAreEqual(attributes, other.attributes)) return false;
            if (!ListExt.ListsAreEqual(entries, other.entries)) return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DataScheme)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (SchemeName != null ? SchemeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (attributes != null ? attributes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (entries != null ? entries.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(DataScheme left, DataScheme right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DataScheme left, DataScheme right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}