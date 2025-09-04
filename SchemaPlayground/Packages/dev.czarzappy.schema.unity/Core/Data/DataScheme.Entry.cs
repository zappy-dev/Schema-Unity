using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Schema.Core.Logging;

namespace Schema.Core.Data
{
    public partial class DataScheme
    {
        #region Entry Operations
        
        #region Entry Mutations
        
        public DataEntry CreateNewEmptyEntry()
        {
            var entry = new DataEntry();
            foreach (var attribute in attributes)
            {
                SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue());
            }
            
            entries.Add(entry);
            IsDirty = true;
            return entry;
        }

        public SchemaResult AddEntry(DataEntry newEntry, bool runDataValidation = true)
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
                if (runDataValidation)
                {
                    var isValidRes = attribute.CheckIfValidData(entryValue);
                    if (isValidRes.Failed)
                    {
                        return isValidRes;
                    }
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
            
            entries.Add(newEntry);
            IsDirty = true;
            return SchemaResult.Pass($"Added {newEntry}", this);
        }

        public SchemaResult DeleteEntry(DataEntry entry)
        {
            bool result = entries.Remove(entry);
            IsDirty = result;
            return SchemaResult.CheckIf(result, 
                errorMessage: "Could not delete entry",
                successMessage: "Removed entry");
        }

        #endregion
        
        #region Entry Retrieval Queries
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

        #endregion

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
            return Move(entry, targetIndex, entries);
        }

        public SchemaResult SwapEntries(int srcIndex, int dstIndex)
        {
            return Swap(srcIndex, dstIndex, entries);
        }

        #endregion

        #endregion

        /// <summary>
        /// Retrieve the order index for the given entry
        /// </summary>
        /// <param name="entry">Entry to index</param>
        /// <returns>-1 if entry does not exist in the given scheme</returns>
        public int GetEntryIndex(DataEntry entry)
        {
            return entries.IndexOf(entry);
        }
    }
}