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
        
        public DataEntry CreateNewEmptyEntry(SchemaContext context)
        {
            var entry = new DataEntry();
            foreach (var attribute in attributes)
            {
                SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue(), allowIdentifierUpdate: true, context: context);
            }
            
            entries.Add(entry);
            SetDirty(context, true);
            return entry;
        }

        public SchemaResult AddEntry(SchemaContext context, DataEntry newEntry, bool runDataValidation = true)
        {
            using var _ = new SchemeContextScope(ref context, this);
            Logger.LogDbgVerbose($"Adding {newEntry}...", context);
            if (newEntry is null)
            {
                return SchemaResult.Fail(context, "Entry cannot be null");
            }
            
            // TODO: Validate that a data entry has all of the expected attributes and add default attribute values if not present
            // Also fail if unexpected attribute values are encountered? 

            foreach (var kvp in newEntry)
            {
                string attributeName = kvp.Key;
                // Don't need to validate invalid attribute names, since adding new entry data already does that.

                if (!GetAttributeByName(attributeName, context: context).Try(out var attribute))
                {
                    // TODO: Figure out a better solution for adding entries that contain unknown attributes
                    Logger.LogWarning($"Skipping validation for unknown attribute: {attributeName}");
                    return SchemaResult.Fail(context, $"No matching attribute found for '{kvp.Key}'");
                }

                var entryValue = kvp.Value;
                if (runDataValidation)
                {
                    var isValidRes = attribute.IsValidValue(context, entryValue);
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

                newEntry.SetData(context, attribute.AttributeName, attribute.CloneDefaultValue());
            }
            
            entries.Add(newEntry);
            SetDirty(context, true);
            return SchemaResult.Pass($"Added {newEntry}", context);
        }

        public SchemaResult DeleteEntry(SchemaContext context, DataEntry entry)
        {
            bool result = entries.Remove(entry);
            if (result)
            {
                SetDirty(context, true);
            }
            return SchemaResult.CheckIf(context,
                result, errorMessage: "Could not delete entry", successMessage: "Removed entry");
        }

        #endregion
        
        #region Entry Retrieval Queries

        // Let this context get defaulted, since it is used by the runtime
        public SchemaResult<DataEntry> GetEntry(Func<DataEntry, bool> entryFilter, SchemaContext context = default)
        {
            var entry = entries.FirstOrDefault(entryFilter);
            return SchemaResult<DataEntry>.CheckIf(entry != null, entry, "Entry not found", context: context);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataEntry GetEntry(int entryIndex)
        {
            return entries[entryIndex];
        }

        #endregion

        #region Entry Ordering Operations
        
        public SchemaResult MoveUpEntry(SchemaContext context, DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            var newIdx = entryIdx - 1;
            return SwapEntries(context, entryIdx, newIdx);
        }

        public SchemaResult MoveDownEntry(SchemaContext context, DataEntry entry)
        {
            var entryIdx = entries.IndexOf(entry);
            var newIdx = entryIdx + 1;
            return SwapEntries(context, entryIdx, newIdx);
        }

        public SchemaResult MoveEntry(SchemaContext context, DataEntry entry, int targetIndex)
        {
            return Move(context, entry, targetIndex, entries);
        }

        public SchemaResult SwapEntries(SchemaContext context, int srcIndex, int dstIndex)
        {
            return Swap(context, srcIndex, dstIndex, entries);
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