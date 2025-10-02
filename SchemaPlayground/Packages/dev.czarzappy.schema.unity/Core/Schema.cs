using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;
using Schema.Core.Ext;
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
        public static SchemaResult<IEnumerable<string>> GetAllSchemes(SchemaContext context)
        {
            var res = SchemaResult<IEnumerable<string>>.New(context);
            var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<IEnumerable<string>>();
            }

            // return dataSchemes.Keys;

            lock (manifestOperationLock)
            {
                if (!GetManifestScheme().Try(out var manifestScheme, out var manifestError))
                {
                    return manifestError.CastError<IEnumerable<string>>();
                }

                return res.Pass(manifestScheme.GetAllSchemeNames());
            }
        }

        public static SchemaResult<int> GetNumAvailableSchemes(SchemaContext context)
        {
            var allSchemesRes = GetAllSchemes(context);
            if (!allSchemesRes.Try(out var schemes, out var error))
            {
                return error.CastError<int>();
            }

            return SchemaResult<int>.Pass(schemes.Count());
        }

        public static IEnumerable<DataScheme> GetSchemes(SchemaContext context)
        {
            if (!GetAllSchemes(context).Try(out var schemes, out var error))
            { 
                yield break;
            }
            
            foreach (var schemeName in schemes)
            {
                if (GetScheme(context, schemeName).Try(out var scheme))
                {
                    yield return scheme;
                }
            }
        }

        /// <summary>
        /// Schema is not initialized until the Storage system is set and a Manifest Scheme is laoded.
        /// </summary>
        public static SchemaResult IsInitialized(SchemaContext context)
        {
            if (!GetStorage(context).Try(out var _, out var storageErr))
            {
                return storageErr.Cast();
            }

            if (string.IsNullOrWhiteSpace(ProjectPath))
            {
                return Fail(context, "No project path specified.");
            }

            return CheckIf(context, _loadedManifestScheme != null, "No Manifest Scheme Loaded");
        }
        
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
            loadedSchemes.Clear();
            manifestImportPath = String.Empty;
            nextManifestScheme = null;
            _loadedManifestScheme = null;
        }
        
        #endregion

        #region Interface Commands

        public static SchemaResult<bool> DoesSchemeExist(SchemaContext ctx, string schemeName)
        {
            var res = SchemaResult<bool>.New(ctx);
            if (string.IsNullOrWhiteSpace(schemeName))
            {
                return res.Fail("Scheme name is empty");
            }
            
            return res.Pass(loadedSchemes.ContainsKey(schemeName));
        }

        // TODO support async
        public static SchemaResult<DataScheme> GetScheme(SchemaContext context, string schemeName)
        {
            // var isInitRes = IsInitialized(context);
            // if (isInitRes.Failed)
            // {
            //     return isInitRes.CastError<DataScheme>();
            // }
            
            var success = loadedSchemes.TryGetValue(schemeName, out var scheme);
            var errorMessage = $"No Scheme '{schemeName}' loaded.";
            if (schemeName == Manifest.MANIFEST_SCHEME_NAME)
            {
                errorMessage =
                    $"No Manifest Scheme loaded. To load a manifest, call {nameof(Schema)}.{nameof(InitializeTemplateManifestScheme)} to initialize an empty project " +
                    $"or call either {nameof(Schema)}.{nameof(LoadManifestFromPath)} to load a Manifest from file.";
            }
            return SchemaResult<DataScheme>.CheckIf(success, scheme, 
                errorMessage: errorMessage,
                successMessage: $"Scheme '{schemeName}' is loaded.", context);
        }

        public static SchemaResult<DataScheme> GetOwnerSchemeForAttribute(SchemaContext context, AttributeDefinition searchAttr)
        {
            var res = SchemaResult<DataScheme>.New(context);
                      var isInitRes = IsInitialized(context);
            if (isInitRes.Failed)
            {
                return isInitRes.CastError<DataScheme>();
            }
            
            var ownerScheme = loadedSchemes.Values.FirstOrDefault(scheme =>
            {
                return scheme.GetAttribute(attr => attr.Equals(searchAttr)).Try(out _);
            });
            
            return res.CheckIf(ownerScheme != null, ownerScheme, $"No owner scheme found for attribute: {searchAttr}");
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
            using var _ = new AttributeContextScope(ref context, identifierAttribute);
            
            // 1. Update the identifier value in the specified scheme
            if (!GetScheme(context, schemeName).Try(out var targetScheme))
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
            foreach (var scheme in GetSchemes(context))
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