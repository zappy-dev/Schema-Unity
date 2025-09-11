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
            });
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.FilePath),
                DataType = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true), // TOOD: Set base content path?
                DefaultValue = DataType.Text.DefaultValue,
                IsIdentifier = false,
            });
            // this is going to cause a data migration....
            templateManifestScheme.AddAttribute(new AttributeDefinition
            {
                AttributeName = nameof(ManifestEntry.PublishTarget),
                DataType = new TextDataType(ManifestScheme.PublishTarget.DEFAULT.ToString()),
                IsIdentifier = false
            });
            
            var manifestSelfEntry = new DataEntry(new Dictionary<string, object>
            {
                { nameof(ManifestEntry.SchemeName), Manifest.MANIFEST_SCHEME_NAME },
                { nameof(ManifestEntry.FilePath), "" },
                { nameof(ManifestEntry.PublishTarget), ManifestScheme.PublishTarget.DEFAULT }
            });
            // since we are creating a new 
            templateManifestScheme.AddEntry(manifestSelfEntry);
            return new ManifestScheme(templateManifestScheme);
        }
    }
}