using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Schema.Core.Ext;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    // Representing a data scheme in memory
    [Serializable]
    public partial class DataScheme : ResultGenerator
    {
        public override SchemaContext Context => new SchemaContext
        {
            Scheme = this,
        };
        
        #region Fields and Properties
        
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
        public bool IsManifest => Manifest.MANIFEST_SCHEME_NAME.Equals(SchemeName);
        
        [JsonIgnore]
        public bool HasIdentifierAttribute
            => GetAttribute(a => a.IsIdentifier).Passed;

        /// <summary>
        /// Indicates whether there is a change to this in-memory data that should get persiste.
        /// </summary>
        private bool isDirty = false;

        [JsonIgnore]
        public bool IsDirty
        {
            get => isDirty;
            set
            {
                Logger.LogDbgVerbose($"IsDirty=>{value}", Context);
                isDirty = value;
            }
        }

        #endregion
        
        #region Lifecycle
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

        /// <summary>
        /// Loads this DataScheme with the central service, without an import path
        /// </summary>
        /// <param name="overwriteExisting"></param>
        /// <returns></returns>
        public SchemaResult Load(bool overwriteExisting = false)
        {
            return Schema.LoadDataScheme(this, overwriteExisting: overwriteExisting);
        }

        #endregion

        // Methods for adding/updating entries, etc.

        public override string ToString()
        {
#if SCHEMA_DEBUG
            return ToString(true);
#else
            return ToString(false);
#endif
        }

        public string ToString(bool verbose)
        {
            StringBuilder stringBuilder = new StringBuilder();
            Write(stringBuilder, verbose);
            return stringBuilder.ToString();
        }
        
        public void Write(StringBuilder stringBuilder, bool verbose)
        {
            var isDirty = IsDirty ? "*" : "";
            stringBuilder.Append($"DataScheme '{SchemeName}' [{AttributeCount}x{AllEntries.Count()}]{isDirty} ({RuntimeHelpers.GetHashCode(this)})");
            if (verbose)
            {
                
                // var handle = GCHandle.Alloc(this, GCHandleType.Pinned);
                //
                // try
                // {
                //     IntPtr address = handle.AddrOfPinnedObject();
                // }
                // finally
                // {
                //     handle.Free();                           // ALWAYS free the handle
                // }
                stringBuilder.AppendLine();
                foreach (var attribute in attributes)
                {
                    stringBuilder.Append($"{attribute.AttributeName},");
                }
                stringBuilder.AppendLine();
                foreach (var entry in entries)
                {
                    stringBuilder.AppendLine($"{entry}");
                }
            }
        }
        
        #region Utilities
        
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
            IsDirty = true;
            return SchemaResult.Pass("Successfully swapped entry", this);
        }
        
        public SchemaResult Move<T>(T element, int targetIndex, List<T> data)
        {
            if (targetIndex < 0 || targetIndex >= data.Count)
            {
                return SchemaResult.Fail($"Target index {targetIndex} is out of range.", this);
            }
            
            var entryIdx = data.IndexOf(element);
            if (entryIdx == -1)
            {
                return SchemaResult.Fail("Element not found", this);
            }
            if (entryIdx == targetIndex)
            {
                return SchemaResult.Fail("Element cannot be the same as the target.", this);
            }
            data.RemoveAt(entryIdx);
            data.Insert(targetIndex, element);

            isDirty = true;
            return SchemaResult.Pass($"Moved {element} from {entryIdx} to {targetIndex}", this);
        }
        
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

        /// <summary>
        /// Updates all entries in this scheme that reference a given identifier value from another scheme.
        /// </summary>
        /// <param name="referencedScheme">The name of the referenced scheme.</param>
        /// <param name="referencedAttribute">The name of the referenced identifier attribute.</param>
        /// <param name="oldValue">The old identifier value to be replaced in references.</param>
        /// <param name="newValue">The new identifier value to set in references.</param>
        /// <returns>The number of references updated in this scheme.</returns>
        public int UpdateReferencesToIdentifier(string referencedScheme, string referencedAttribute, object oldValue, object newValue)
        {
            int updated = 0;
            foreach (var attr in attributes)
            {
                if (attr.DataType is ReferenceDataType refType &&
                    refType.ReferenceSchemeName == referencedScheme &&
                    refType.ReferenceAttributeName == referencedAttribute)
                {
                    foreach (var entry in entries)
                    {
                        var value = entry.GetDataAsString(attr.AttributeName);
                        if (Equals(value, oldValue?.ToString()))
                        {
                            entry.SetData(attr.AttributeName, newValue);
                            updated++;
                        }
                    }
                }
            }

            if (updated > 0)
            {
                isDirty = true;
            }
            return updated;
        }

        /// <summary>
        /// Sets data on a DataEntry, enforcing identifier immutability unless explicitly allowed.
        /// </summary>
        /// <param name="entry">The DataEntry to update.</param>
        /// <param name="attributeName">The attribute name to update.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="allowIdentifierUpdate">If true, allows updating identifier values (should only be true for centralized update method).</param>
        /// <returns>A SchemaResult indicating success or failure.</returns>
        public SchemaResult SetDataOnEntry(DataEntry entry, string attributeName, object value, bool allowIdentifierUpdate = false)
        {
            var attrResult = GetAttribute(attributeName);
            if (!attrResult.Try(out var attr))
                return SchemaResult.Fail($"Attribute '{attributeName}' not found in scheme '{SchemeName}'.");
            if (attr.IsIdentifier && !allowIdentifierUpdate)
            {
                return SchemaResult.Fail($"Direct mutation of identifier attribute '{attributeName}' is not allowed. Use Schema.UpdateIdentifierValue instead.");
            }

            Logger.LogDbgVerbose($"SetDataOnEntry, entry: {entry}, {attributeName}=>{value}", context: Context);
            IsDirty = true;
            return entry.SetData(attributeName, value);
        }
    }
}