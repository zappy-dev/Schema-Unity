using System.CommandLine;
using Schema.Core;
using Schema.Core.IO;
using Schema.Core.Logging;

namespace Schema.CLI;

/// <summary>
/// Entry point for the Schema CLI. Provides commands to inspect and operate on Schema projects.
/// </summary>
internal static class Program
{
    private struct CLICommandContext
    {
        public string ProjectRoot;
        public string ManifestPath;
    }

    #region Constants
    
    private static readonly Option<string> ProjectOption = new("--project", "-p")
    {
        Description = "Path to project root (defaults to current directory)"
    };
    private static readonly Option<string> ManifestOption = new("--manifest", "-m")
    {
        Description = "Path to explicit Manifest.json (overrides discovery)"
    };
    private static readonly Option<bool> VerboseOption = new("--verbose", "-v")
    {
        Description = "Enable verbose logging"
    };

    #endregion
    
    private static int Main(string[] args)
    {
        Logger.SetLogger(new ConsoleLogger());
        RootCommand rootCommand = new("Command-Line Interface for Schema");

        Command statusCommand = new("status", "Show available schemes for a project by locating and loading Manifest.json");
        rootCommand.Subcommands.Add(statusCommand);
        statusCommand.Options.Add(ProjectOption);
        statusCommand.Options.Add(ManifestOption);
        statusCommand.Options.Add(VerboseOption);
        statusCommand.SetAction(result =>
        {
            var exitCode = Initialize(result, out var ctx);
            if (exitCode != 0)
            {
                return exitCode;
            }
            
            return RunStatus(ctx);
        });

        Command viewCommand = new("view", "View a given schema as a table");
        viewCommand.Options.Add(ManifestOption);
        viewCommand.Options.Add(ProjectOption);
        viewCommand.Options.Add(VerboseOption);
        Argument<string> schemeNameArg = new("Scheme Name");
        viewCommand.Arguments.Add(schemeNameArg);
        rootCommand.Subcommands.Add(viewCommand);
        viewCommand.SetAction((result) =>
        {
            int exitCode = Initialize(result, out var ctx);
            if (exitCode != 0) return exitCode;

            string schemeName = result.GetValue(schemeNameArg);
            
            return RunView(ctx, schemeName);
        });
        
        ParseResult parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static int Initialize(ParseResult result, out CLICommandContext ctx)
    {
        var verbose = result.GetValue(VerboseOption);
        if (verbose)
        {
            SchemaResultSettings.Instance.LogStackTrace = true;
            SchemaResultSettings.Instance.LogFailure = true;
        }
        var projectRoot = result.GetValue(ProjectOption) ?? Directory.GetCurrentDirectory();
        var explicitManifestPath = result.GetValue(ManifestOption);

        ctx = default;
        
        try
        {
            var manifestPath = FindManifestPath(projectRoot, explicitManifestPath);
            if (manifestPath == null)
            {
                Console.Error.WriteLine($"Could not find Manifest.json under '{projectRoot}'.\n" +
                                        "Use --project to point at a project root or --manifest to an explicit file.");
                return 3;
            }

            var inferredProjectRoot = InferProjectRootFromManifest(manifestPath);

            InitializeSchemaForCli(inferredProjectRoot);

            var context = new SchemaContext { Driver = "CLI_Status" };
            var loadRes = Schema.Core.Schema.LoadManifestFromPath(context, manifestPath);
            if (!loadRes.Passed)
            {
                Console.Error.WriteLine($"Failed to load manifest:\n-{loadRes.Message}");
                return 4;
            }

            if (!loadRes.Result.Equals(Schema.Core.Schema.ManifestLoadStatus.FULLY_LOADED)) {
                Console.Error.WriteLine($"Failed to load manifest:\n-{loadRes.Message}");
            }
            
            ctx = new CLICommandContext
            {
                ProjectRoot = inferredProjectRoot,
                ManifestPath = manifestPath
            };
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 99;
        }
    }

    #region Commands
    /// <summary>
    /// Executes the status command.
    /// </summary>
    /// <param name="args">Arguments following the "status" command.</param>
    private static int RunStatus(CLICommandContext cliCtx)
    {
        var ctx = new SchemaContext
        {
            Driver = "CLI_Status"
        };
            
        if (!Schema.Core.Schema.GetAllSchemes(ctx).Try(out var allSchemes, out var schemeErr))
        {
            Console.Error.WriteLine(schemeErr);
            return 2;
        }
            
        var schemes = allSchemes.ToList();
            
        Logger.Log($"Project Root: {cliCtx.ProjectRoot}");
        Logger.Log($"Manifest:     {cliCtx.ManifestPath}");
        Logger.Log($"Schemes ({schemes.Count}):");
        foreach (var name in schemes)
        {
            if (Schema.Core.Schema.GetScheme(ctx, name).Try(out var scheme, out var error)) {
                Logger.Log($"  - {scheme.ToString()}");
            }
            else {
                Logger.Log($"  - {name} - {error.Message}");
            }
        }
            
        return 0;
    }

    private static int RunView(CLICommandContext cliCtx, string schemeName)
    {
        var ctx = new SchemaContext
        {
            Driver = "CLI_View"
        };
        
        if (!Schema.Core.Schema.GetScheme(ctx, schemeName).Try(out var scheme, out var error))
        {
            Console.Error.WriteLine(error.Message);
            return -1;
        }
            
        Logger.Log(scheme.PrintTableView().ToString());

        return 0;
    }

    #endregion

    #region CLI Bootstrapping
    /// <summary>
    /// Initializes Schema core for CLI context with a local filesystem and sets the project path.
    /// </summary>
    /// <param name="projectRoot">The root directory of the project (parent of the Content folder).</param>
    private static void InitializeSchemaForCli(string projectRoot)
    {
        Schema.Core.Schema.SetStorage(StorageFactory.GetEditorStorage());
        Schema.Core.Schema.ProjectPath = projectRoot;
        Schema.Core.Schema.Reset();
    }

    /// <summary>
    /// Attempts to find a Manifest.json given a project root and/or explicit manifest override.
    /// Prefers "Content/Manifest.json" directly under the project root, otherwise searches recursively.
    /// </summary>
    /// <param name="projectRoot">Directory to search under.</param>
    /// <param name="explicitManifestPath">Explicit manifest file path (if provided).</param>
    /// <returns>Absolute file path to the manifest, or null if not found.</returns>
    private static string FindManifestPath(string projectRoot, string explicitManifestPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitManifestPath))
        {
            var abs = Path.GetFullPath(explicitManifestPath);
            return File.Exists(abs) ? abs : null;
        }

        var root = string.IsNullOrWhiteSpace(projectRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(projectRoot);

        // Preferred location: <root>/Content/Manifest.json
        var preferred = Path.Combine(root, "Content", "Manifest.json");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        // Fallback: any Manifest.json under the root
        var candidates = Directory.EnumerateFiles(root, "Manifest.json", SearchOption.AllDirectories)
            .OrderBy(p => Path.GetFileName(Path.GetDirectoryName(p))?.Equals("Content", StringComparison.OrdinalIgnoreCase) == true ? 0 : 1)
            .ThenBy(p => p.Length)
            .ToList();

        return candidates.FirstOrDefault();
    }

    /// <summary>
    /// Infers the project root from a manifest path. If the manifest is inside a "Content" folder,
    /// the project root is the parent of that folder; otherwise, the manifest's directory is used.
    /// </summary>
    /// <param name="manifestPath">Absolute path to Manifest.json</param>
    private static string InferProjectRootFromManifest(string manifestPath)
    {
        var manifestDir = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrEmpty(manifestDir))
        {
            return Directory.GetCurrentDirectory();
        }

        var dirName = Path.GetFileName(manifestDir);
        if (string.Equals(dirName, "Content", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(manifestDir);
            return parent?.FullName ?? manifestDir;
        }

        return manifestDir;
    }
    
    #endregion
}