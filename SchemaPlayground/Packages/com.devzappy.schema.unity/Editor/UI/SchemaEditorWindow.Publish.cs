using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Ext;
using Schema.Core.Schemes;
using Schema.Runtime;
using Schema.Runtime.Type;
using UnityEditor;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        private SchemaResult PublishScheme(SchemaContext context, string schemeName)
        {
            LogDbgVerbose($"Publishing {schemeName}");
            var schemeEntry = GetManifestEntryForScheme(schemeName);
            if (!schemeEntry.Try(out ManifestEntry manifestEntry) ||
                !GetScheme(context, schemeName).Try(out var schemeToPublish))
            {
                return Fail(context, $"Could not find manifest entry for scheme to publish, scheme: {schemeName}");
            }

            // TODO: Figure out how to better enumerate this, enforce Manifest scheme attribute is valid
            // Problem: Manifest Scheme loads first, before any other scheme, so if it has a reference data type attribute to another scheme, that is invalid
            // could skip validation and validate after load?
            // But seems hacky
            bool hasValidPublishTarget = Enum.TryParse<ManifestScheme.PublishTarget>(manifestEntry.PublishTarget, 
                out var publishTarget);

            if (!hasValidPublishTarget)
            {
                return Fail(context, $"Invalid publish target for scheme to publish, scheme: {schemeName}, found target: {manifestEntry.PublishTarget}, " +
                            $"expected a valid type from {nameof(ManifestScheme.PublishTarget)}.");
            }
            
            // publishing data
            bool isSuccess = true;
            switch (publishTarget)
            {
                case ManifestScheme.PublishTarget.SCRIPTABLE_OBJECT:
                {
                    // HACK: Assumes ID column is the asset guid for an underlying scriptable object to publish to
                    if (!schemeToPublish.GetIdentifierAttribute().Try(out var idAttr))
                    {
                        return Fail(context, "No identifier attribute found for scheme to publish");
                    }
                    
                    foreach (var entry in schemeToPublish.AllEntries)
                    {
                        var publishRes = PublishEntryToScriptableObject(context, schemeToPublish, entry, idAttr);
                        isSuccess &= publishRes.Passed;
                    }
                    break;
                }
                case ManifestScheme.PublishTarget.RESOURCES:
                {
                    var publishRes = PublishToResources(schemeToPublish, context);
                    isSuccess = publishRes.Passed;
                } 
                    break;
            }

            if (!GetStorage(context).Try(out var storage, out var storageError)) return storageError.Cast();
            
            // Publish code
            var exportRes = storage.CSharpSchemeStorageFormat.Export(schemeToPublish, context);
            if (exportRes.Failed)
            {
                return exportRes;
            }
            
            // Publish asset links

            foreach (var entry in schemeToPublish.AllEntries)
            {
                foreach (var attribute in schemeToPublish.GetAttributes())
                {
                    // find attributes that map to unity assets
                    if (!(attribute.DataType is UnityAssetDataType unityAssetDataType))
                    {
                        continue;
                    }

                    var assetGuid = entry.GetDataAsGuid(attribute.AttributeName).ToAssetGuid();

                    // No asset to link
                    if (string.IsNullOrEmpty(assetGuid))
                    {
                        continue;
                    }

                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, unityAssetDataType.ObjectType);

                    var assetLink = new AssetLink(assetGuid, assetPath, asset);
                    var assetLinker = UnityAssetLinker.Instance;
                    if (assetLinker == null)
                    {
                        assetLinker = UnityAssetLinker.CreateAsset();
                    }
                    assetLinker.AddAssetLink(assetLink);
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return Pass("Published assets");
        }

        /// <summary>
        /// Publishes a given scheme to an associated Resources file
        /// </summary>
        /// <param name="schemeToPublish"></param>
        /// <returns></returns>
        private SchemaResult PublishToResources(DataScheme originalSchemeToPublish, SchemaContext context)
        {
            if (!GetStorage(context).Try(out var storage, out var storageError)) return storageError.Cast();

            var storageFormat = storage.DefaultSchemaPublishFormat;
            string schemaPublishPath = $"{SchemaRuntime.DEFAULT_RESOURCE_PUBLISH_PATH}/{originalSchemeToPublish.SchemeName}.{storageFormat.Extension}";

            // prepare a new data scheme to publish
            var publishScheme = new DataScheme(originalSchemeToPublish.SchemeName);

            // first clone published attributes
            var publishedAttributes = originalSchemeToPublish.GetAttributes(a => a.ShouldPublish).ToList();
            foreach (var publishedAttr in publishedAttributes)
            {
                publishScheme.AddAttribute(context, publishedAttr.Clone() as AttributeDefinition);
            }
            
            // second clone data entries, stripped for only published entries
            if (!originalSchemeToPublish.GetEntries(context: context).Try(out var entries, out var error)) return error.Cast();
            
            foreach (var entry in entries)
            {
                var publishedEntry = new DataEntry();

                var copyRes = BulkResult(
                    entries: publishedAttributes,
                    operation: (publishedAttr) =>
                        publishedEntry.SetData(context, publishedAttr.AttributeName, entry.GetDataDirect(publishedAttr)),
                    errorMessage: $"Failed to publish entry: {entry}",
                    context: context);

                if (copyRes.Failed)
                {
                    return copyRes;
                }

                publishScheme.AddEntry(context, publishedEntry);
            }
            
            var publishRes = storageFormat.SerializeToFile(context, schemaPublishPath, publishScheme);

            return publishRes;
        }

        /// <summary>
        /// Publishes a given data entry to an associated Scriptable Object
        /// </summary>
        /// <param name="schemeToPublish"></param>
        /// <param name="entry"></param>
        /// <param name="idAttr"></param>
        /// <returns></returns>
        private SchemaResult PublishEntryToScriptableObject(SchemaContext context, DataScheme schemeToPublish, DataEntry entry, AttributeDefinition idAttr)
        {
            throw new NotImplementedException("Publishing Scriptable Objects is not implemented");
            var assetGuid = entry.GetDataAsGuid(idAttr.AttributeName);

            if (!AssetUtils.TryLoadAssetFromGUID(assetGuid, null, out var currentAsset))
            {
                return Fail(context,$"Not asset found with guid: {assetGuid}");
            }
                        
            var assetType = currentAsset.GetType();

            var soFields = TypeUtils.GetSerializedFieldsForType(assetType);
            var fieldMap = schemeToPublish.GetAttributes()
                .ToDictionary(attr => attr.AttributeName, attr => soFields.FirstOrDefault(field => field.Name == attr.AttributeName));
            
            // BUG: Referenced Data Type stops matching Reference'd Data Type
            foreach (var kvp in entry)
            {
                var attrName = kvp.Key;
                var value = kvp.Value;
                
                // HACK: Skip Asset GUID Id field
                if (attrName == idAttr.AttributeName) continue;
                            
                var field = fieldMap[attrName];
                if (!schemeToPublish.GetAttribute(attrName).Try(out var attr))
                {
                    LogError($"No attribute found for entry key: {attrName}");
                    continue;
                }
                            
                try
                {
                    object mappedValue = null;
                    if (field.FieldType.IsEnum)
                    {
                        mappedValue = Enum.Parse(field.FieldType, value?.ToString() ?? string.Empty);
                    }
                    else
                    {
                        switch (attr.DataType)
                        {
                            // HACK: I know in this case the one reference data type we have is an asset guid reference, just resolve that reference
                            case ReferenceDataType refDataType:

                                if (!schemeToPublish.GetEntry(searchEntry => searchEntry.GetData(refDataType.ReferenceAttributeName) == value)
                                        .Try(out var refEntry))
                                {
                                    LogDbgError($"Failed to find referenced entry for value: {value} ({value?.GetType()})");
                                    continue;
                                }

                                var refGuid = refEntry.GetDataAsGuid(refDataType.ReferenceAttributeName);
                                if (AssetUtils.TryLoadAssetFromGUID(refGuid, null, out var refAsset))
                                {
                                    mappedValue = refAsset;
                                }
                                break;
                            default:
                                mappedValue = value;
                                break;
                        }
                    }
                    field.SetValue(currentAsset, mappedValue);
                    LogDbgVerbose($"{field.Name}=>{mappedValue}");
                }
                catch (Exception e)
                {
                    LogError(e.Message);
                }
            }
            
            LogDbgVerbose($"Saving changes to asset: {currentAsset}");
            EditorUtility.SetDirty(currentAsset);

            return Pass($"Saving changes to asset: {currentAsset}");
        }


        private SchemaResult BulkPublishSchemes(SchemaContext context, IReadOnlyList<string> schemeNames)
        {
            using var progressScope = new ProgressScope("Schema - Publish All");
            // TODO: need a way of generating an aggregate schema result
            int progress = 1;
            int total = schemeNames.Count;

            var bulkRes =  BulkResult(
                entries: schemeNames,
                haltOnError: true, // Avoid publishing bad code if a Scheme fails to publish
                operation: (schemeName) =>
                {
                    progressScope.Progress($"Publishing Scheme '{schemeName}' ({progress} / {total})",
                        progress * 1.0f / total);
                    progress++;
                    return PublishScheme(context, schemeName);
                },
                errorMessage: "Failed to publish all schemes. Check console logs for more details.");
            
            EditorUtility.DisplayDialog("Schema", (bulkRes.Passed) ? "Successfully published all Schemes!" : bulkRes.Message, "Ok");
            return bulkRes;
        }
        
        private SchemaResult PublishAllSchemes(SchemaContext context)
        {
            // get all loaded schemes and publish in topological order.
            if (!DataScheme.TopologicalSortByReferences(context, LoadedSchemes.Values)
                .Try(out var schemesToPublish, out var sortErr))
            {
                return sortErr.Cast();
            }
            
            return BulkPublishSchemes(context, schemesToPublish.Select(s => s.SchemeName).ToList());
        }
    }
}