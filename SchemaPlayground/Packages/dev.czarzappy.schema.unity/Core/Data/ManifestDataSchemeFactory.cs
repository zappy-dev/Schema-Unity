using System.Collections.Generic;
using System.IO;
using Schema.Core.Schemes;

namespace Schema.Core.Data
{
    public static class ManifestDataSchemeFactory
    {
        /// <summary>
        /// Builds a template manifest schema with default attributes and a self-entry.
        /// </summary>
        /// <returns>A <see cref="DataScheme"/> representing the template manifest schema.</returns>
        public static ManifestScheme BuildTemplateManifestSchema(SchemaContext context, 
            string defaultScriptExportPath,
            string importPath)
        {
            var templateManifestScheme = new DataScheme(Manifest.MANIFEST_SCHEME_NAME);
            templateManifestScheme.AddAttribute(context, new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.SchemeName),
                DataType = DataType.Text.Clone() as DataType,
                DefaultValue = DataType.Text.CloneDefaultValue(),
                AttributeToolTip = "The name of the scheme",
                IsIdentifier = true,
                ShouldPublish = true,
            });
            templateManifestScheme.AddAttribute(context, new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.FilePath),
                DataType = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true), // TOOD: Set base content path?
                DefaultValue = DataType.Text.CloneDefaultValue(),
                ColumnWidth = AttributeDefinition.DefaultColumnWidth * 2,
                AttributeToolTip = "Relative path to the staging file from this scheme",
                IsIdentifier = false,
                ShouldPublish = true,
            });
            // this is going to cause a data migration....
            templateManifestScheme.AddAttribute(context, new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.PublishTarget),
                DataType = DataType.Text.Clone() as DataType,
                DefaultValue = ManifestScheme.PublishTarget.DEFAULT.ToString(), // TODO: Support enum values
                AttributeToolTip = "Type of output format to use for publishing this data scheme",
                IsIdentifier = false,
                ShouldPublish = false,
            });
            templateManifestScheme.AddAttribute(context, new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.CSharpExportPath),
                DataType = DataType.Folder_RelativePaths.Clone() as DataType,
                DefaultValue = defaultScriptExportPath,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth * 2,
                AttributeToolTip = "Destination folder for generated C# code for this scheme",
                IsIdentifier = false,
                ShouldPublish = false,
            });
            templateManifestScheme.AddAttribute(context, new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.CSharpNamespace),
                DataType = DataType.Text.Clone() as DataType,
                DefaultValue = DataType.Text.CloneDefaultValue(),
                AttributeToolTip = "Namespace for generated C# code for this scheme",
                IsIdentifier = false,
                ShouldPublish = false,
            });
            
            var manifestSelfEntry = new DataEntry(new Dictionary<string, object>
            {
                { nameof(ManifestEntry.SchemeName), Manifest.MANIFEST_SCHEME_NAME },
                { nameof(ManifestEntry.FilePath), importPath },
                { nameof(ManifestEntry.PublishTarget), ManifestScheme.PublishTarget.DEFAULT.ToString() }, // TODO: support enum values
                { nameof(ManifestEntry.CSharpExportPath), defaultScriptExportPath },
                { nameof(ManifestEntry.CSharpNamespace), DataType.Text.CloneDefaultValue() }
            });
            // skipping data validation because we don't want to resolve file system paths...
            templateManifestScheme.AddEntry(context, manifestSelfEntry, runDataValidation: false);
            return new ManifestScheme(templateManifestScheme);
        }
    }
}