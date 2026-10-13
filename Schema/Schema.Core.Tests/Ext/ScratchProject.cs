using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Schemes;

namespace Schema.Core.Tests.Ext;

/// <summary>
/// A throwaway project on disk (a temp directory with a Content folder), so a test can write tables and a manifest
/// and then load them through <see cref="Schema.LoadManifestFromPath"/>, the way the editor and the CLI do.
/// Dispose restores the global Schema state that the shared test fixture set up.
/// </summary>
public sealed class ScratchProject : IDisposable
{
    public static readonly SchemaContext Context = new()
    {
        Driver = nameof(ScratchProject),
    };

    private readonly string? previousProjectPath;
    private readonly List<DataScheme> schemes = new();

    public string Root { get; }
    public string ContentDir => Path.Combine(Root, "Content");
    public string ManifestPath => Path.Combine(ContentDir, "Manifest.json");

    public ScratchProject()
    {
        previousProjectPath = Schema.ProjectPath;
        Root = Path.Combine(Path.GetTempPath(), "schema-scratch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(ContentDir);
    }

    /// <summary>Adds a table to the project. It is written to disk by <see cref="Save"/>.</summary>
    public DataScheme Add(DataScheme scheme)
    {
        schemes.Add(scheme);
        return scheme;
    }

    /// <summary>Writes every added table and a manifest that lists them, in the order they were added.</summary>
    public void Save()
    {
        Schema.Reset();
        var storage = StorageFactory.GetEditorStorage();
        Schema.SetStorage(storage);
        Schema.ProjectPath = Root;
        Schema.InitializeTemplateManifestScheme(Context).AssertPassed();

        var manifest = Schema.GetManifestScheme(Context).Result;
        foreach (var scheme in schemes)
        {
            manifest.AddManifestEntry(Context, scheme.SchemeName, ManifestScheme.PublishTarget.DEFAULT,
                $"Content/{scheme.SchemeName}.json").Cast().AssertPassed();
            storage.JsonSchemeStorageFormat.SerializeToFile(Context, Path.Combine(ContentDir, $"{scheme.SchemeName}.json"), scheme)
                .AssertPassed();
        }

        storage.JsonSchemeStorageFormat.SerializeToFile(Context, ManifestPath, manifest._).AssertPassed();
    }

    /// <summary>Starts from nothing and loads the manifest from disk.</summary>
    public SchemaResult<Schema.ManifestLoadStatus> Load()
    {
        Schema.Reset();
        Schema.SetStorage(StorageFactory.GetEditorStorage());
        Schema.ProjectPath = Root;
        return Schema.LoadManifestFromPath(Context, ManifestPath);
    }

    public void Dispose()
    {
        Schema.Reset();
        Schema.ProjectPath = previousProjectPath;
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // a leftover temp directory is not worth failing a test for
        }
    }
}

public static class ScratchSchemes
{
    /// <summary>A table with a Text identifier column "ID" and the given identifier values.</summary>
    public static DataScheme Table(string name, params string[] ids)
    {
        var scheme = new DataScheme(name);
        scheme.AddAttribute(ScratchProject.Context, "ID", DataType.Text, isIdentifier: true).AssertPassed();
        foreach (var id in ids)
        {
            scheme.AddEntry(ScratchProject.Context, new DataEntry { { "ID", id, ScratchProject.Context } }).AssertPassed();
        }

        return scheme;
    }

    /// <summary>Adds a Text column and one cell per row, in row order, to a table built by <see cref="Table"/>.</summary>
    public static void AddTextColumn(DataScheme scheme, string column, params string?[] cells)
    {
        scheme.AddAttribute(ScratchProject.Context, column, DataType.Text).AssertPassed();
        var i = 0;
        foreach (var entry in scheme.AllEntries)
        {
            entry.SetData(ScratchProject.Context, column, cells[i++]);
        }

        scheme.SetDirty(ScratchProject.Context, false);
    }
}
