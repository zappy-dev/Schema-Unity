using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Ext;
using Schema.Core.Publish;
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

            if (!IsValidScheme(context, schemeToPublish).Try(out var validationErr))
            {
                return validationErr;
            }
            
            // What a publish writes is the project's call: the built-in outputs its publish settings leave on, and
            // then its own publish steps. A project that bakes the table into an asset of its own switches both
            // built-in outputs off (Project Settings > Schema Publish) and publishes through a step.
            if (!SchemaPublishSettingsStore.Load(context).Try(out var publishSettings, out var settingsError))
            {
                return settingsError.Cast();
            }

            if (!GetStorage(context).Try(out var storage, out var storageError)) return storageError.Cast();

            var publishRes = SchemaPublishPipeline.Run(
                new SchemaPublishRequest(context, schemeToPublish),
                publishSettings,
                SchemaPublishStepRegistry.Steps,
                publishResourcesJson: request => PublishSchemeData(request, publishTarget),
                generateCSharp: request => storage.CSharpSchemeStorageFormat.Export(request.Scheme, request.Context));

            if (publishRes.Failed)
            {
                return publishRes;
            }
            
            // Publish asset links
            var assetLinker = UnityAssetLinker.Instance;
            if (assetLinker == null)
            {
                assetLinker = UnityAssetLinker.CreateAsset();
            }
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
                    assetLinker.AddAssetLink(context, assetLink);
                }
            }
            
            AssetDatabase.SaveAssetIfDirty(assetLinker);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return Pass(publishRes.Message, context);
        }

        /// <summary>
        /// Writes the scheme's data to the target its Manifest entry names. The whole of this is skipped when the
        /// project has switched the Resources JSON off: see <see cref="SchemaPublishSettings.PublishResourcesJson"/>.
        /// </summary>
        private SchemaResult PublishSchemeData(SchemaPublishRequest request, ManifestScheme.PublishTarget publishTarget)
        {
            var context = request.Context;
            var schemeToPublish = request.Scheme;

            switch (publishTarget)
            {
                case ManifestScheme.PublishTarget.SCRIPTABLE_OBJECT:
                {
                    // HACK: Assumes ID column is the asset guid for an underlying scriptable object to publish to
                    if (!schemeToPublish.GetIdentifierAttribute().Try(out var idAttr))
                    {
                        return Fail(context, "No identifier attribute found for scheme to publish");
                    }

                    return BulkResult(schemeToPublish.AllEntries,
                        entry => PublishEntryToScriptableObject(context, schemeToPublish, entry, idAttr),
                        errorMessage: $"Failed to publish scheme '{request.SchemeName}' to its scriptable objects",
                        context: context);
                }
                case ManifestScheme.PublishTarget.RESOURCES:
                    return PublishToResources(schemeToPublish, context);
                default:
                    return Pass($"No data to write for publish target {publishTarget}", context);
            }
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

            // prepare a new data scheme to publish: the published columns and every row. A row that cannot be added fails the publish.
            if (!originalSchemeToPublish.CreatePublishedCopy(context).Try(out var publishScheme, out var copyError)) return copyError.Cast();
            
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
        #pragma warning disable CS0162
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
        #pragma warning restore CS0162


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