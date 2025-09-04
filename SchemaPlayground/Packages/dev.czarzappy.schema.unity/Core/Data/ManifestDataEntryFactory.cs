using Schema.Core.Schemes;

namespace Schema.Core.Data
{
    public static class ManifestDataEntryFactory
    {
        public static ManifestEntry Build(ManifestScheme manifestScheme, string schemeName, string importFilePath = null)
        {
            var newSchemeManifestEntry = new DataEntry();
            newSchemeManifestEntry.SetData(nameof(ManifestEntry.SchemeName), schemeName);
                    
            if (!string.IsNullOrWhiteSpace(importFilePath))
            {
                // TODO: Record import path or give option to clone / copy file to new content path?
                newSchemeManifestEntry.SetData(nameof(ManifestEntry.FilePath), importFilePath);
            }
            
            return new ManifestEntry(manifestScheme._, newSchemeManifestEntry);
        }
    }
}