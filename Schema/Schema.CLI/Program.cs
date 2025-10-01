using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.IO;

/// <summary>
/// Entry point for the Schema CLI. Provides commands to inspect and operate on Schema projects.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point. Supports the "status" command which locates a Manifest.json, loads it,
    /// and reports available schemes for the project.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    private static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args))
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        switch (command)
        {
            case "status":
                return RunStatus(args.Skip(1).ToArray());
            case "view":
                return RunView(args.Skip(1).ToArray());
            default:
                Console.Error.WriteLine($"Unknown command: {command}\n");
                PrintUsage();
                return 1;
        }
    }

    private static int RunCommandBase(string[] args, Func<CLICommandContext, int> runCommand)
    {
        // Defaults
        string projectRoot = Directory.GetCurrentDirectory();
        string explicitManifestPath = null;

        // Simple option parsing: --project/-p, --manifest/-m, or a single positional path
        var argQueue = new Queue<string>(args);
        while (argQueue.Count > 0)
        {
            var token = argQueue.Dequeue();
            switch (token)
            {
                case "--project":
                case "-p":
                    if (argQueue.Count == 0)
                    {
                        Console.Error.WriteLine("Missing value for --project");
                        return 2;
                    }
                    projectRoot = argQueue.Dequeue();
                    break;
                case "--manifest":
                case "-m":
                    if (argQueue.Count == 0)
                    {
                        Console.Error.WriteLine("Missing value for --manifest");
                        return 2;
                    }
                    explicitManifestPath = argQueue.Dequeue();
                    break;
                case "--help":
                case "-h":
                    PrintStatusUsage();
                    return 0;
                default:
                    // Treat unknown token as a positional project path if none set yet
                    if (Directory.Exists(token) && projectRoot == null)
                    {
                        projectRoot = token;
                    }
                    else if (File.Exists(token) && explicitManifestPath == null)
                    {
                        explicitManifestPath = token;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unrecognized argument: {token}");
                        PrintStatusUsage();
                        return 2;
                    }
                    break;
            }
        }

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
                Console.Error.WriteLine($"Failed to load manifest: {loadRes.Message}");
                return 4;
            }

            if (!loadRes.Result.Equals(Schema.Core.Schema.ManifestLoadStatus.FULLY_LOADED)) {
                Console.Error.WriteLine($"Failed to load manifest: {loadRes.Message}");
                return 4;
            }

            return runCommand(new CLICommandContext
            {
                ProjectRoot = inferredProjectRoot,
                ManifestPath = manifestPath
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 99;
        }
    }

    private struct CLICommandContext
    {
        public string ProjectRoot;
        public string ManifestPath;
    }

    /// <summary>
    /// Executes the status command.
    /// </summary>
    /// <param name="args">Arguments following the "status" command.</param>
    private static int RunStatus(string[] args)
    {
        return RunCommandBase(args, (ctx) =>
        {
            var schemes = Schema.Core.Schema.AllSchemes.ToList();
            
            Console.WriteLine($"Project Root: {ctx.ProjectRoot}");
            Console.WriteLine($"Manifest:     {ctx.ManifestPath}");
            Console.WriteLine($"Schemes ({schemes.Count}):");
            foreach (var name in schemes)
            {
                if (Schema.Core.Schema.GetScheme(name).Try(out var scheme, out var error)) {
                    Console.WriteLine($"  - {scheme.ToString()}");
                }
                else {
                    Console.WriteLine($"  - {name} - {error.Message}");
                }
            }
            
            return 0;
        });
        
        // // Defaults
        // string projectRoot = Directory.GetCurrentDirectory();
        // string explicitManifestPath = null;
        //
        // // Simple option parsing: --project/-p, --manifest/-m, or a single positional path
        // var argQueue = new Queue<string>(args);
        // while (argQueue.Count > 0)
        // {
        //     var token = argQueue.Dequeue();
        //     switch (token)
        //     {
        //         case "--project":
        //         case "-p":
        //             if (argQueue.Count == 0)
        //             {
        //                 Console.Error.WriteLine("Missing value for --project");
        //                 return 2;
        //             }
        //             projectRoot = argQueue.Dequeue();
        //             break;
        //         case "--manifest":
        //         case "-m":
        //             if (argQueue.Count == 0)
        //             {
        //                 Console.Error.WriteLine("Missing value for --manifest");
        //                 return 2;
        //             }
        //             explicitManifestPath = argQueue.Dequeue();
        //             break;
        //         case "--help":
        //         case "-h":
        //             PrintStatusUsage();
        //             return 0;
        //         default:
        //             // Treat unknown token as a positional project path if none set yet
        //             if (Directory.Exists(token) && projectRoot == null)
        //             {
        //                 projectRoot = token;
        //             }
        //             else if (File.Exists(token) && explicitManifestPath == null)
        //             {
        //                 explicitManifestPath = token;
        //             }
        //             else
        //             {
        //                 Console.Error.WriteLine($"Unrecognized argument: {token}");
        //                 PrintStatusUsage();
        //                 return 2;
        //             }
        //             break;
        //     }
        // }
        //
        // try
        // {
        //     var manifestPath = FindManifestPath(projectRoot, explicitManifestPath);
        //     if (manifestPath == null)
        //     {
        //         Console.Error.WriteLine($"Could not find Manifest.json under '{projectRoot}'.\n" +
        //                                 "Use --project to point at a project root or --manifest to an explicit file.");
        //         return 3;
        //     }
        //
        //     var inferredProjectRoot = InferProjectRootFromManifest(manifestPath);
        //
        //     InitializeSchemaForCli(inferredProjectRoot);
        //
        //     var context = new SchemaContext { Driver = "CLI_Status" };
        //     var loadRes = Schema.Core.Schema.LoadManifestFromPath(context, manifestPath);
        //     if (!loadRes.Passed)
        //     {
        //         Console.Error.WriteLine($"Failed to load manifest: {loadRes.Message}");
        //         return 4;
        //     }
        //
        //     if (!loadRes.Result.Equals(Schema.Core.Schema.ManifestLoadStatus.FULLY_LOADED)) {
        //         Console.Error.WriteLine($"Failed to load manifest: {loadRes.Message}");
        //         return 4;
        //     }
        //
        //     var schemes = Schema.Core.Schema.AllSchemes.ToList();
        //
        //     Console.WriteLine($"Project Root: {inferredProjectRoot}");
        //     Console.WriteLine($"Manifest:     {manifestPath}");
        //     Console.WriteLine($"Schemes ({schemes.Count}):");
        //     foreach (var name in schemes)
        //     {
        //         if (Schema.Core.Schema.GetScheme(name).Try(out var scheme, out var error)) {
        //             Console.WriteLine($"  - {scheme.ToString()}");
        //         }
        //         else {
        //             Console.WriteLine($"  - {name} - {error.Message}");
        //         }
        //     }
        //
        //     return 0;
        // }
        // catch (Exception ex)
        // {
        //     Console.Error.WriteLine($"Unexpected error: {ex.Message}");
        //     return 99;
        // }
    }

    private static int RunView(string[] args)
    {
        return RunCommandBase(args, (ctx) =>
        {
            if (Schema.Core.Schema.GetScheme("Entities").Try(out var scheme, out var error))
            {
                var maxWidth = new Dictionary<string, int>();
                foreach (var attributeDefinition in scheme.GetAttributes())
                {
                    maxWidth[attributeDefinition.AttributeName] = attributeDefinition.AttributeName.Length;
                }
                
                // pad entries
                foreach (var entry in  scheme.AllEntries)
                {
                    foreach (var attribute in scheme.GetAttributes())
                    {
                        int newMaxWidth = entry.GetDataAsString(attribute.AttributeName).Length;
                        if (maxWidth.TryGetValue(attribute.AttributeName, out var value))
                        {
                            newMaxWidth = (value > newMaxWidth) ? value :  newMaxWidth;
                        }

                        maxWidth[attribute.AttributeName] = newMaxWidth;
                    }
                }
                
                var tableSB = new StringBuilder();

                // table header, top
                int numAttrs = scheme.AttributeCount;

                int attrIdx = 0;
                
                void DrawTableRow(char start, char end, char bridge, char gap)
                {
                    attrIdx = 0;
                    tableSB.Append(start);
                    tableSB.Append(gap);
                    foreach (var attribute in scheme.GetAttributes())
                    {
                        tableSB.Append(string.Empty.PadRight(maxWidth[attribute.AttributeName], gap));
                        if (++attrIdx < numAttrs)
                        {
                            tableSB.Append(gap);
                            tableSB.Append(bridge);
                            tableSB.Append(gap);
                        }
                    }
                    tableSB.Append(gap);
                    tableSB.AppendLine(end.ToString());
                }

                DrawTableRow('┌', '┐', '┬', '─');
                
                // table header, attributes
                tableSB.Append("│ ");
                attrIdx = 0;
                foreach (var attribute in 
                         scheme.GetAttributes())
                {
                    tableSB.Append(attribute.AttributeName.PadRight(maxWidth[attribute.AttributeName]));
                    if (++attrIdx < numAttrs) tableSB.Append(" │ ");
                }
                tableSB.AppendLine(" │");
                
                // table header, break
                DrawTableRow('├', '┤', '┼', '─');

                // table entries
                foreach (var entry in scheme.AllEntries)
                {
                    tableSB.Append("│ ");
                    attrIdx = 0;
                    foreach (var attribute in scheme.GetAttributes())
                    {
                        tableSB.Append(entry.GetDataAsString(attribute.AttributeName).PadRight(maxWidth[attribute.AttributeName]));
                        if (++attrIdx < numAttrs) tableSB.Append(" │ ");
                    }

                    tableSB.AppendLine(" │");
                }
                
                // footer
                DrawTableRow('└', '┘', '┴', '─');
                
                Console.Write(tableSB.ToString());

                return 0;
            }
            else
            {
                return -1;
            }
        });
    }
    
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

    /// <summary>
    /// Returns true if help was requested.
    /// </summary>
    private static bool IsHelp(string[] args)
    {
        return args.Contains("--help") || args.Contains("-h");
    }

    /// <summary>
    /// Prints general CLI usage help text.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Schema CLI\n");
        Console.WriteLine("Usage:");
        Console.WriteLine("  schema status [--project <path>] [--manifest <file>]\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  status   Show available schemes for a project by locating and loading Manifest.json");
        Console.WriteLine();
        PrintStatusUsage();
    }

    /// <summary>
    /// Prints usage for the status command.
    /// </summary>
    private static void PrintStatusUsage()
    {
        Console.WriteLine("status options:");
        Console.WriteLine("  -p, --project  Path to project root (defaults to current directory)");
        Console.WriteLine("  -m, --manifest Path to explicit Manifest.json (overrides discovery)");
        Console.WriteLine("  -h, --help     Show this help for status");
    }
}
