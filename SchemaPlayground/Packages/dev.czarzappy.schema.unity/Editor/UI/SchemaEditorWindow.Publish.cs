using System;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Schemes;
using Schema.Runtime;
using UnityEditor;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        private SchemaResult PublishScheme(string schemeName)
        {
            LogDbgVerbose($"Publishing {schemeName}");
            var schemeEntry = GetManifestEntryForScheme(schemeName);
            if (!schemeEntry.Try(out ManifestEntry manifestEntry) ||
                !GetScheme(schemeName).Try(out var schemeToPublish))
            {
                return Fail($"Could not find manifest entry for scheme to publish, scheme: {schemeName}");
            }

            // TODO: Figure out how to better enumerate this, enforce Manifest scheme attribute is valid
            // Problem: Manifest Scheme loads first, before any other scheme, so if it has a reference data type attribute to another scheme, that is invalid
            // could skip validation and validate after load?
            // But seems hacky
            bool hasValidPublishTarget = Enum.TryParse<ManifestScheme.PublishTarget>(manifestEntry.PublishTarget, 
                out var publishTarget);

            if (!hasValidPublishTarget)
            {
                return Fail($"Invalid publish target for scheme to publish, scheme: {schemeName}, found target: {manifestEntry.PublishTarget}, " +
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
                        return Fail("No identifier attribute found for scheme to publish", schemeToPublish.Context);
                    }
                    
                    foreach (var entry in schemeToPublish.AllEntries)
                    {
                        var publishRes = PublishEntryToScriptableObject(schemeToPublish, entry, idAttr);
                        isSuccess &= publishRes.Passed;
                    }
                    break;
                }
                case ManifestScheme.PublishTarget.RESOURCES:
                {
                    var publishRes = PublishToResources(schemeToPublish);
                    isSuccess = publishRes.Passed;
                } 
                    break;
            }
            
            // Publish code
            Storage.CSharpStorageFormat.Export(schemeToPublish);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return Pass("Published assets");
        }

        /// <summary>
        /// Publishes a given scheme to an associated Resources file
        /// </summary>
        /// <param name="schemeToPublish"></param>
        /// <returns></returns>
        private SchemaResult PublishToResources(DataScheme schemeToPublish)
        {
            var storageFormat = Storage.DefaultSchemaPublishFormat;
            string schemaPublishPath = $"{SchemaRuntime.DEFAULT_RESOURCE_PUBLISH_PATH}/{schemeToPublish.SchemeName}.{storageFormat.Extension}";

            var publishRes = storageFormat.SerializeToFile(schemaPublishPath, schemeToPublish);

            return publishRes;
        }

        /// <summary>
        /// Publishes a given data entry to an associated Scriptable Object
        /// </summary>
        /// <param name="schemeToPublish"></param>
        /// <param name="entry"></param>
        /// <param name="idAttr"></param>
        /// <returns></returns>
        private SchemaResult PublishEntryToScriptableObject(DataScheme schemeToPublish, DataEntry entry, AttributeDefinition idAttr)
        {
            var assetGuid = entry.GetDataAsGuid(idAttr.AttributeName);

            if (!AssetUtils.TryLoadAssetFromGUID(assetGuid, out var currentAsset))
            {
                return Fail($"Not asset found with guid: {assetGuid}");
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

                                if (!schemeToPublish.TryGetEntry(searchEntry =>
                                        searchEntry.GetData(refDataType.ReferenceAttributeName) == value, out var refEntry))
                                {
                                    LogDbgError($"Failed to find referenced entry for value: {value} ({value?.GetType()})");
                                    continue;
                                }

                                var refGuid = refEntry.GetDataAsGuid(refDataType.ReferenceAttributeName);
                                if (AssetUtils.TryLoadAssetFromGUID(refGuid, out var refAsset))
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

        private SchemaResult PublishAllSchemes()
        {
            // TODO: need a way of generating an aggregate schema result
            bool success = true;
            foreach (var allScheme in AllSchemes)
            {
                var result = PublishScheme(allScheme);
                success &= result.Passed;
            }
            
            return CheckIf(success, "Failed to publish all schemes. Check console logs for more details.");
        }
    }
}