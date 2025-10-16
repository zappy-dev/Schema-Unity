using System;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Ext;
using Schema.Core.Logging;
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
        internal static readonly PublishConfig UnityEditorPublishConfig = new PublishConfig
        {
            PostPublish = PostPublishAssetLinking,
            ResolveExportPath = (format, exportFileName) =>
            {
                var extParts = format.Extension.Split('.');
                var lastExt = extParts[extParts.Length - 1];
                Logger.LogDbgVerbose($"Export {exportFileName}, last ext: {lastExt}");
                            
                return EditorUtility.SaveFilePanel($"Save {format.Extension.ToUpper()}", DefaultContentDirectory, 
                    exportFileName, 
                    lastExt);
            }
        };
        
        private static void PostPublishAssetLinking(SchemaContext ctx, DataScheme schemeToPublish)
        {
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
                    assetLinker.AddAssetLink(ctx, assetLink);
                }
            }
            
            AssetDatabase.SaveAssetIfDirty(assetLinker);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

    }
}