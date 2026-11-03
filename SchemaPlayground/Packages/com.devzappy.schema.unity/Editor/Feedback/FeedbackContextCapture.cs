using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>Reads the live Editor into <see cref="FeedbackContextInputs"/>. Every read is guarded: a field that cannot be read is left out.</summary>
    internal static class FeedbackContextCapture
    {
        /// <param name="window">The open Schema Editor, or null when none is open.</param>
        /// <param name="includeSchemeData">Serialise the open scheme into the context (only when the person ticked it).</param>
        public static FeedbackContextInputs Capture(SchemaEditorWindow window, bool includeSchemeData)
        {
            var inputs = new FeedbackContextInputs
            {
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString(),
            };

            inputs.ProjectPath = Path.GetFullPath(Application.dataPath + "/..");
            inputs.ProjectName = Path.GetFileName(inputs.ProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            ReadPackage(inputs);
            ReadSchemaPaths(inputs);
            inputs.ConsoleLines = ConsoleTail.Shared.Snapshot();

            if (window != null)
            {
                inputs.SelectedScheme = window.FeedbackSelectedSchemeName;
                if (window.TryGetFeedbackCell(out var attribute, out var row, out var value))
                {
                    inputs.SelectedCell = new FeedbackCell { Attribute = attribute, Row = row, Value = value };
                }

                if (includeSchemeData) inputs.SchemeJson = window.SerializeSelectedSchemeForFeedback();
            }

            return inputs;
        }

        private static void ReadPackage(FeedbackContextInputs inputs)
        {
            try
            {
                var package = PackageInfo.FindForAssembly(typeof(SchemaEditorWindow).Assembly);
                if (package == null) return;

                inputs.PackageVersion = package.version;
                // A git URL dependency carries its commit; a file: dependency into a working tree does not.
                inputs.PackageCommit = package.git != null && !string.IsNullOrEmpty(package.git.hash)
                    ? package.git.hash
                    : GitHead.TryRead(package.resolvedPath);
            }
            catch (Exception)
            {
                // PackageManager can be mid-resolve; the report is still worth sending without it
            }
        }

        private static void ReadSchemaPaths(FeedbackContextInputs inputs)
        {
            try
            {
                var manifest = global::Schema.Core.Schema.ManifestImportPath;
                if (!string.IsNullOrEmpty(manifest))
                {
                    inputs.ManifestPath = manifest;
                    inputs.ContentPath = Path.GetDirectoryName(manifest);
                }
            }
            catch (Exception)
            {
                // not loaded yet
            }
        }
    }
}
