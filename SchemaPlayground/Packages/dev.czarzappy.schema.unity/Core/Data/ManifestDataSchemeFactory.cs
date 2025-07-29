using System.Collections.Generic;
using Schema.Core.Schemes;

namespace Schema.Core.Data
{
    public static class ManifestDataSchemeFactory
    {
        

        /// <summary>
        /// Builds a template manifest schema with default attributes and a self-entry.
        /// </summary>
        /// <returns>A <see cref="DataScheme"/> representing the template manifest schema.</returns>
        public static ManifestScheme BuildTemplateManifestSchema()
        {
            var templateManifestScheme = new DataScheme(Manifest.MANIFEST_SCHEME_NAME);
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.SchemeName),
                DataType = DataType.Text,
                DefaultValue = DataType.Text.DefaultValue,
                IsIdentifier = true,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth
            });
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.FilePath),
                DataType = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true), // TOOD: Set base content path?
                DefaultValue = DataType.Text.DefaultValue,
                IsIdentifier = false,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
            });
            
            var manifestSelfEntry = new DataEntry(new Dictionary<string, object>
            {
                { nameof(ManifestEntry.SchemeName), Manifest.MANIFEST_SCHEME_NAME },
                { nameof(ManifestEntry.FilePath), "" },
            });
            // since we are creating a new 
            templateManifestScheme.AddEntry(manifestSelfEntry);
            return new ManifestScheme(templateManifestScheme);
        }
    }
}