using System;
using System.IO;
using Newtonsoft.Json;
using Schema.Core.IO;

namespace Schema.Core.Publish
{
    /// <summary>
    /// What a Publish writes, for one project. Both built-in outputs are on by default, so a project that never
    /// touches this keeps the behaviour it had. A project that publishes through its own
    /// <see cref="ISchemaPublishStep"/> can switch them off and have Publish do only what the project does.
    ///
    /// Kept in the project's own content folder (<see cref="PathFor"/>, next to the Manifest), so it travels with
    /// the project and is committed with it, rather than being a per-machine preference.
    /// </summary>
    public sealed class SchemaPublishSettings
    {
        /// <summary>The settings file, in the project's content folder beside the Manifest.</summary>
        public const string FileName = "PublishSettings.json";

        /// <summary>
        /// Write the published table to the scheme's publish target: for the default RESOURCES target, the filtered
        /// JSON copy under <c>Assets/Plugins/Schema/Resources</c> that the runtime loads. Off: the publish writes no
        /// data file and the runtime loader finds nothing, which is what a project that bakes its own asset wants.
        /// </summary>
        public bool PublishResourcesJson { get; set; } = true;

        /// <summary>
        /// Generate the strongly-typed C# wrappers for the scheme into the Manifest's <c>CSharpExportPath</c>.
        /// Off: no generated code is written or refreshed, and existing generated files are left alone.
        /// </summary>
        public bool GenerateCSharp { get; set; } = true;

        /// <summary>True when neither built-in output is on: only the project's own publish steps will run.</summary>
        [JsonIgnore]
        public bool IsEveryBuiltInOutputOff => !PublishResourcesJson && !GenerateCSharp;

        /// <summary>The settings a project gets when it has never said otherwise: both outputs on.</summary>
        public static SchemaPublishSettings Defaults() => new SchemaPublishSettings();

        /// <summary>Where the settings live for a project rooted at <paramref name="projectPath"/>.</summary>
        public static string PathFor(string projectPath) =>
            Path.Combine(projectPath ?? string.Empty, global::Schema.Core.Schema.DefaultContentDirectory, FileName);

        /// <summary>
        /// Reads the settings for a project. A project with no settings file gets <see cref="Defaults"/>, so this is
        /// never a reason a publish cannot run. A file that is there but cannot be read or parsed fails, naming the
        /// path: a publish that silently ignored it could write files the project had switched off.
        /// </summary>
        public static SchemaResult<SchemaPublishSettings> Load(SchemaContext context, IFileSystem fileSystem, string filePath)
        {
            if (fileSystem == null)
            {
                return SchemaResult<SchemaPublishSettings>.Fail($"No file system to read publish settings from, path: {filePath}", context);
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return SchemaResult<SchemaPublishSettings>.Fail("No path given for the project's publish settings", context);
            }

            if (fileSystem.FileExists(context, filePath).Failed)
            {
                return SchemaResult<SchemaPublishSettings>.Pass(Defaults(),
                    $"No publish settings at {filePath}, using the defaults (publish the Resources JSON and generate C#).", context);
            }

            if (!fileSystem.ReadAllText(context, filePath).Try(out var json, out var readError))
            {
                return readError.CastError<SchemaPublishSettings>();
            }

            try
            {
                var settings = JsonConvert.DeserializeObject<SchemaPublishSettings>(json);
                if (settings == null)
                {
                    return SchemaResult<SchemaPublishSettings>.Fail(
                        $"Publish settings at {filePath} are empty. Delete the file to go back to the defaults.", context);
                }

                return SchemaResult<SchemaPublishSettings>.Pass(settings, $"Loaded publish settings from {filePath}", context);
            }
            catch (Exception e)
            {
                return SchemaResult<SchemaPublishSettings>.Fail(
                    $"Could not read the publish settings at {filePath}: {e.Message}. Fix the file, or delete it to go back to the defaults.", context);
            }
        }

        /// <summary>Writes the settings for a project, creating the content folder if it is not there yet.</summary>
        public SchemaResult Save(SchemaContext context, IFileSystem fileSystem, string filePath)
        {
            if (fileSystem == null)
            {
                return SchemaResult.Fail(context, $"No file system to write publish settings to, path: {filePath}");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return SchemaResult.Fail(context, "No path given for the project's publish settings");
            }

            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                var writeRes = fileSystem.WriteAllText(context, filePath, json);
                return writeRes.Failed ? writeRes : SchemaResult.Pass($"Saved publish settings to {filePath}", context);
            }
            catch (Exception e)
            {
                return SchemaResult.Fail(context, $"Could not write the publish settings to {filePath}: {e.Message}");
            }
        }

        public override string ToString() =>
            $"{nameof(SchemaPublishSettings)}[{nameof(PublishResourcesJson)}={PublishResourcesJson}, {nameof(GenerateCSharp)}={GenerateCSharp}]";
    }
}
