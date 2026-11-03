using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>The host state a report carries. Plain values, so the assembly step is testable without an Editor.</summary>
    internal sealed class FeedbackContextInputs
    {
        public string ProjectPath;
        public string ProjectName;
        public string UnityVersion;
        public string Platform;
        public string PackageVersion;
        public string PackageCommit;
        public string ManifestPath;
        public string ContentPath;
        public string SelectedScheme;
        public FeedbackCell SelectedCell;
        public List<string> ConsoleLines = new List<string>();

        /// <summary>The open scheme serialised as JSON; null unless the person ticked "include the open scheme's data".</summary>
        public string SchemeJson;
    }

    internal static class FeedbackContext
    {
        /// <summary>
        /// Phase 1 reports are not repaired. repairkit records this as the report's hold_reason and holds any
        /// repair for review; it is added to every submit whatever the caller passed.
        /// </summary>
        public const string HoldForReview = "phase 1: report only";

        public const int MaxSchemeJsonChars = 400 * 1024;

        /// <summary>The <c>context</c> object of a <c>submit</c>: snake_case keys, empty values left out.</summary>
        public static JObject Build(FeedbackContextInputs inputs)
        {
            var context = new JObject();
            Put(context, "project_path", inputs.ProjectPath);
            Put(context, "project_name", inputs.ProjectName);
            Put(context, "unity_version", inputs.UnityVersion);
            Put(context, "platform", inputs.Platform);
            Put(context, "package_version", inputs.PackageVersion);
            Put(context, "package_commit", inputs.PackageCommit);
            Put(context, "manifest_path", inputs.ManifestPath);
            Put(context, "content_path", inputs.ContentPath);
            Put(context, "selected_scheme", inputs.SelectedScheme);

            if (inputs.SelectedCell != null)
            {
                context["selected_cell"] = new JObject
                {
                    ["attribute"] = inputs.SelectedCell.Attribute,
                    ["row"] = inputs.SelectedCell.Row,
                    ["value"] = inputs.SelectedCell.Value,
                };
            }

            if (inputs.ConsoleLines != null && inputs.ConsoleLines.Count > 0)
            {
                context["console_lines"] = new JArray(inputs.ConsoleLines);
                context["console_lines_note"] = ConsoleTail.Note;
            }

            if (!string.IsNullOrEmpty(inputs.SchemeJson))
            {
                if (inputs.SchemeJson.Length > MaxSchemeJsonChars)
                {
                    context["scheme_json"] = inputs.SchemeJson.Substring(0, MaxSchemeJsonChars);
                    context["scheme_json_truncated"] = true;
                }
                else
                {
                    context["scheme_json"] = inputs.SchemeJson;
                }
            }

            context["hold_for_review"] = HoldForReview;
            return context;
        }

        private static void Put(JObject context, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) context[key] = value;
        }
    }
}
