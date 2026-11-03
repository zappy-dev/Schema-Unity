using Schema.Core;
using Schema.Core.Publish;
using static Schema.Core.Schema;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// Reads and writes the project's <see cref="SchemaPublishSettings"/> through Schema's own storage, from the
    /// one place the editor knows the project root: <see cref="Schema.Core.Schema.ProjectPath"/>.
    /// </summary>
    internal static class SchemaPublishSettingsStore
    {
        /// <summary>Where the settings file is for the project that is open, for messages and the settings UI.</summary>
        public static string SettingsPath => SchemaPublishSettings.PathFor(ProjectPath);

        public static SchemaResult<SchemaPublishSettings> Load(SchemaContext context)
        {
            if (!GetStorage(context).Try(out var storage, out var storageError))
            {
                return storageError.CastError<SchemaPublishSettings>();
            }

            return SchemaPublishSettings.Load(context, storage.FileSystem, SettingsPath);
        }

        public static SchemaResult Save(SchemaContext context, SchemaPublishSettings settings)
        {
            if (!GetStorage(context).Try(out var storage, out var storageError))
            {
                return storageError.Cast();
            }

            return settings.Save(context, storage.FileSystem, SettingsPath);
        }
    }
}
