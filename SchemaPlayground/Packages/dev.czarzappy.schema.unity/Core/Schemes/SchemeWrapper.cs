using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    /// <summary>
    /// Base class for strongly-typed wrappers around a <see cref="DataScheme"/>.
    /// </summary>
    public abstract class SchemeWrapper<TEntry> where TEntry : EntryWrapper
    {
        private protected readonly DataScheme DataScheme;
        
        /// <summary>
        /// Underlying data scheme
        /// </summary>
        public DataScheme _ => DataScheme;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemeWrapper{TEntry}"/>.
        /// </summary>
        /// <param name="dataScheme">The underlying data scheme to wrap.</param>
        public SchemeWrapper(DataScheme dataScheme)
        {
            DataScheme = dataScheme;
        }
        
        /// <summary>
        /// Gets the number of entries in the scheme.
        /// </summary>
        public int EntryCount => DataScheme.EntryCount;

        /// <summary>
        /// Creates a new entry wrapper for a given <see cref="DataEntry"/>.
        /// </summary>
        protected abstract TEntry EntryFactory(DataScheme dataScheme, DataEntry dataEntry);

        #region Interface!

        /// <summary>
        /// Retrieves a <see cref="DataScheme"/> by name using a wrapper-friendly context.
        /// </summary>
        /// <param name="schemeName">The scheme name.</param>
        public static SchemaResult<DataScheme> GetScheme(string schemeName)
        {
            var ctx = new SchemaContext
            {
                Driver = "Codegen_Wrapper",
            };
            
            return Schema.GetScheme(ctx, schemeName);
        }
        
        /// <summary>
        /// Gets an entry wrapper by its zero-based index.
        /// </summary>
        /// <param name="entryIndex">Zero-based entry index.</param>
        public TEntry GetEntryByIndex(int entryIndex)
        {
            return EntryFactory(DataScheme, DataScheme.GetEntry(entryIndex));
        }
        
        /// <summary>
        /// Gets an entry wrapper by its identifier value.
        /// </summary>
        /// <param name="entryId">The identifier value.</param>
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
        
        /// <summary>
        /// Finds an entry using a predicate against typed entry wrappers.
        /// </summary>
        /// <param name="entryFilter">Predicate to match entries.</param>
        public SchemaResult<TEntry> GetEntry(Func<TEntry, bool> entryFilter)
        {
            TEntry foundEntry = GetEntries().FirstOrDefault(entryFilter);
            return SchemaResult<TEntry>.CheckIf(foundEntry != default, foundEntry, "Entry not found");
        }

        /// <summary>
        /// Enumerates all entries as typed wrappers.
        /// </summary>
        /// <param name="context">Optional context.</param>
        public IEnumerable<TEntry> GetEntries(SchemaContext context = default)
        {
            return DataScheme.GetEntries(context: context).Select(e => EntryFactory(_, e));
        }
        
        #endregion

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return DataScheme.Equals((obj as  SchemeWrapper<TEntry>).DataScheme);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return DataScheme.GetHashCode();
        }
    }
}