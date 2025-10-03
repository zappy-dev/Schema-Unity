using Schema.Core.Schemes;

namespace Schema.Core.Data
{
    public static class ManifestDataEntryFactory
    {
        /// <summary>
        /// Builds a manifest entry to register with a manifest scheme
        /// </summary>
        /// <param name="context"></param>
        /// <param name="manifestScheme">Manifest Scheme to own new manifest entry</param>
        /// <param name="schemeName">Name of scheme to register to manifest</param>
        /// <param name="publishTarget"></param>
        /// <param name="importFilePath"></param>
        /// <returns></returns>
        public static ManifestEntry Build(
            SchemaContext context,
            ManifestScheme manifestScheme, 
            string schemeName, 
            ManifestScheme.PublishTarget publishTarget, 
            string importFilePath = null)
        {
            var newSchemeManifestEntry = new DataEntry();
            newSchemeManifestEntry.SetData(context, nameof(ManifestEntry.SchemeName), schemeName);
                    
            if (!string.IsNullOrWhiteSpace(importFilePath))
            {
                // TODO: Record import path or give option to clone / copy file to new content path?
                newSchemeManifestEntry.SetData(context, nameof(ManifestEntry.FilePath), importFilePath);
            }
            
            // TODO: Decide on a better solution for mapping enums into Scheme entries
            newSchemeManifestEntry.SetData(context, nameof(ManifestEntry.PublishTarget), publishTarget.ToString());
            
            return new ManifestEntry(manifestScheme._, newSchemeManifestEntry);
        }
    }
}