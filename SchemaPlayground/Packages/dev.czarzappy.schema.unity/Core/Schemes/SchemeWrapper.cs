using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    public abstract class SchemeWrapper<TEntry> where TEntry : EntryWrapper
    {
        private protected readonly DataScheme DataScheme;
        
        /// <summary>
        /// Underlying data scheme
        /// </summary>
        public DataScheme _ => DataScheme;

        public SchemeWrapper(DataScheme dataScheme)
        {
            DataScheme = dataScheme;
        }
        
        public int EntryCount => DataScheme.EntryCount;

        protected abstract TEntry EntryFactory(DataScheme dataScheme, DataEntry dataEntry);

        #region Interface!

        public static SchemaResult<DataScheme> GetScheme(string schemeName)
        {
            var ctx = new SchemaContext
            {
                Driver = "Codegen_Wrapper",
            };
            
            return Schema.GetScheme(schemeName, ctx);
        }
        
        public TEntry GetEntryByIndex(int entryIndex)
        {
            return EntryFactory(DataScheme, DataScheme.GetEntry(entryIndex));
        }
        
        public SchemaResult<TEntry> GetEntryById(object entryId)
        {
            var idAttrRes = _.GetIdentifierAttribute();
            if (!idAttrRes.Try(out var idAttr, out var idAttrError))
            {
                return idAttrError.CastError<TEntry>();
            }

            var idEntryRes = _.GetEntry(e => Equals(e.GetData(idAttr.AttributeName), entryId));
            if (!idEntryRes.Try(out var idEntry, out var idEntryError))
            {
                return idEntryError.CastError<TEntry>();
            }

            return SchemaResult<TEntry>.Pass(EntryFactory(_, idEntry));;
        }
        
        public SchemaResult<TEntry> GetEntry(Func<TEntry, bool> entryFilter)
        {
            TEntry foundEntry = GetEntries().FirstOrDefault(entryFilter);
            return SchemaResult<TEntry>.CheckIf(foundEntry != default, foundEntry, "Entry not found");
        }

        public IEnumerable<TEntry> GetEntries(SchemaContext context = default)
        {
            return DataScheme.GetEntries(context: context).Select(e => EntryFactory(_, e));
        }
        
        #endregion

        public override bool Equals(object obj)
        {
            return DataScheme.Equals((obj as  SchemeWrapper<TEntry>).DataScheme);
        }

        public override int GetHashCode()
        {
            return DataScheme.GetHashCode();
        }
    }
}