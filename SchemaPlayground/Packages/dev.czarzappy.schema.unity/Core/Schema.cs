using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Schema.Core.Data;
using Schema.Core.Logging;
using Schema.Core.Serialization;
using static Schema.Core.SchemaResult;

namespace Schema.Core
{
    public static partial class Schema
    {
        #region Static Fields and Constants
        
        internal static class Context
        {
            public const string DataConversion = "Conversion";
            public const string Manifest = "Manifest";
            public const string Schema = "Schema";
            public const string System = "System";
        }

        internal static readonly Dictionary<string, DataScheme> LoadedSchemes = new Dictionary<string, DataScheme>();
        
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
        private static SchemaResult InitResult;
        
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
            LoadedSchemes.Clear();
            manifestImportPath = String.Empty;
            nextManifestScheme = null;
            loadedManifestScheme = null;

            var initResult = InitializeTemplateManifestScheme();
            IsInitialized = initResult.Passed;
            InitResult = initResult;
        }
        
        #endregion

        #region Interface Commands

        public static bool DoesSchemeExist(string schemeName)
        {
            return LoadedSchemes.ContainsKey(schemeName);
        }

        // TODO support async
        public static SchemaResult<DataScheme> GetScheme(string schemeName, SchemaContext? context = null)
        {
            if (!IsInitialized)
            {
                return SchemaResult<DataScheme>.Fail("Scheme not initialized!", context);
            }
            
            var success = LoadedSchemes.TryGetValue(schemeName, out var scheme);
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
            
            ownerScheme = LoadedSchemes.Values.FirstOrDefault(scheme =>
            {
                return scheme.GetAttribute(attr => attr.Equals(searchAttr)).Try(out _);
            });
            
            return ownerScheme != null;
        }

        /// <summary>
        /// Updates an identifier value in the specified scheme and propagates the change to all referencing entries in all loaded schemes.
        /// </summary>
        /// <param name="schemeName">The name of the scheme containing the identifier to update.</param>
        /// <param name="identifierAttribute">The name of the identifier attribute to update.</param>
        /// <param name="oldValue">The old identifier value to be replaced.</param>
        /// <param name="newValue">The new identifier value to set.</param>
        /// <returns>A SchemaResult indicating success or failure, and the number of references updated.</returns>
        public static SchemaResult UpdateIdentifierValue(string schemeName, string identifierAttribute, object oldValue, object newValue)
        {
            // 1. Update the identifier value in the specified scheme
            if (!GetScheme(schemeName).Try(out var targetScheme))
                return Fail($"Scheme '{schemeName}' not found.");
            
            var entry = targetScheme.AllEntries.FirstOrDefault(e => Equals(e.GetDataAsString(identifierAttribute), oldValue?.ToString()));
            if (entry == null)
                return Fail($"Entry with {identifierAttribute} == '{oldValue}' not found in scheme '{schemeName}'.");
            
            var idUpdateResult = targetScheme.SetDataOnEntry(entry, identifierAttribute, newValue, allowIdentifierUpdate: true);
            if (idUpdateResult.Failed)
                return idUpdateResult;
            int totalUpdated = 0;
            // 2. Propagate to all referencing entries in all loaded schemes
            foreach (var scheme in GetSchemes())
            {
                if (scheme.SchemeName == schemeName)
                    continue;
                totalUpdated += scheme.UpdateReferencesToIdentifier(schemeName, identifierAttribute, oldValue, newValue);
            }
            return Pass($"Updated identifier value from '{oldValue}' to '{newValue}' in '{schemeName}'. Updated {totalUpdated} references.");
        }
        
        #endregion
    }
}