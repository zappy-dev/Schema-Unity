using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;
using Schema.Core.Logging;
using static Schema.Core.SchemaResult;

namespace Schema.Core
{
    public static partial class Schema
    {
        #region Static Fields and Constants

        private static readonly Dictionary<string, DataScheme> loadedSchemes = new Dictionary<string, DataScheme>();
        public static IReadOnlyDictionary<string, DataScheme> LoadedSchemes => loadedSchemes;
        
        /// <summary>
        /// Returns all the available valid scheme names.
        /// </summary>
        public static IEnumerable<string> AllSchemes
        {
            get
            {
                if (!IsInitialized)
                {
                    return Enumerable.Empty<string>();
                }

                // return dataSchemes.Keys;

                lock (manifestOperationLock)
                {
                    if (!GetManifestScheme().Try(out var manifestScheme))
                    {
                        return Enumerable.Empty<string>();
                    }

                    return manifestScheme.GetAllSchemeNames();
                }
            }
        }

        public static int NumAvailableSchemes => AllSchemes.Count();
        public static IEnumerable<DataScheme> GetSchemes()
        {
            foreach (var schemeName in AllSchemes)
            {
                if (GetScheme(schemeName).Try(out var scheme))
                {
                    yield return scheme;
                }
            }
        }
        
        public static bool IsInitialized { get; private set; }
        
        private static readonly object manifestOperationLock = new object();

        #endregion
        
        #region Lifecycle
        
        static Schema()
        {
            // initialize template schemes of data
            Reset();
        }

        public static void Reset()
        {
            Logger.LogDbgWarning("Schema: Resetting...");
            IsInitialized = false;
            loadedSchemes.Clear();
            manifestImportPath = String.Empty;
            nextManifestScheme = null;
            _loadedManifestScheme = null;

            IsInitialized = true;
        }
        
        #endregion

        #region Interface Commands

        public static bool DoesSchemeExist(string schemeName)
        {
            return loadedSchemes.ContainsKey(schemeName);
        }

        // TODO support async
        public static SchemaResult<DataScheme> GetScheme(string schemeName, SchemaContext? context = null)
        {
            if (!IsInitialized)
            {
                return SchemaResult<DataScheme>.Fail("Scheme not initialized!", context);
            }
            
            var success = loadedSchemes.TryGetValue(schemeName, out var scheme);
            return SchemaResult<DataScheme>.CheckIf(success, scheme, 
                errorMessage: $"Scheme '{schemeName}' is not loaded.",
                successMessage: $"Scheme '{schemeName}' is loaded.", context);
        }

        public static bool TryGetSchemeForAttribute(AttributeDefinition searchAttr, out DataScheme ownerScheme)
        {
            if (!IsInitialized)
            {
                ownerScheme = null;
                return false;
            }
            
            ownerScheme = loadedSchemes.Values.FirstOrDefault(scheme =>
            {
                return scheme.GetAttribute(attr => attr.Equals(searchAttr)).Try(out _);
            });
            
            return ownerScheme != null;
        }

        /// <summary>
        /// Updates an identifier value in the specified scheme and propagates the change to all referencing entries in all loaded schemes.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="schemeName">The name of the scheme containing the identifier to update.</param>
        /// <param name="identifierAttribute">The name of the identifier attribute to update.</param>
        /// <param name="oldValue">The old identifier value to be replaced.</param>
        /// <param name="newValue">The new identifier value to set.</param>
        /// <returns>A SchemaResult indicating success or failure, and the number of references updated.</returns>
        public static SchemaResult UpdateIdentifierValue(SchemaContext context, string schemeName,
            string identifierAttribute, object oldValue, object newValue)
        {
            // 1. Update the identifier value in the specified scheme
            if (!GetScheme(schemeName).Try(out var targetScheme))
                return Fail(context, $"Scheme '{schemeName}' not found.");

            return UpdateIdentifierValue(context, targetScheme, identifierAttribute, oldValue, newValue);
        }
        
        /// <summary>
        /// Updates an identifier value in the specified scheme and propagates the change to all referencing entries in all loaded schemes.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="targetScheme">The scheme containing the identifier to update.</param>
        /// <param name="identifierAttribute">The name of the identifier attribute to update.</param>
        /// <param name="oldValue">The old identifier value to be replaced.</param>
        /// <param name="newValue">The new identifier value to set.</param>
        /// <returns>A SchemaResult indicating success or failure, and the number of references updated.</returns>
        public static SchemaResult UpdateIdentifierValue(SchemaContext context, DataScheme targetScheme,
            string identifierAttribute, object oldValue, object newValue)
        {
            var entry = targetScheme.AllEntries.FirstOrDefault(e => Equals(e.GetData(identifierAttribute), oldValue));
            if (entry == null)
                return Fail(context, $"Entry with {identifierAttribute} == '{oldValue}' not found in scheme '{targetScheme}'.");
            
            var idUpdateResult = targetScheme.SetDataOnEntry(entry, identifierAttribute, newValue, allowIdentifierUpdate: true, context: context);
            if (idUpdateResult.Failed)
                return idUpdateResult;
            int totalUpdated = 0;
            // 2. Propagate to all referencing entries in all loaded schemes
            foreach (var scheme in GetSchemes())
            {
                if (scheme.SchemeName == targetScheme.SchemeName)
                    continue;
                totalUpdated += scheme.UpdateReferencesToIdentifier(targetScheme.SchemeName, identifierAttribute, oldValue, newValue, context);
            }
            return Pass($"Updated identifier value from '{oldValue}' to '{newValue}' in '{targetScheme.SchemeName}'. Updated {totalUpdated} references.", context);
        }
        
        #endregion
    }
}