using System;
using System.Linq;
using System.Text;

namespace Schema.Core.Data
{
    public partial class DataEntry
    {
        /// <summary>
        /// Builds a diff report between two entries.
        /// </summary>
        /// <param name="context">The context to use for the diff report.</param>
        /// <param name="diffReport">The diff report to build.</param>
        /// <param name="entryA">The first entry to compare.</param>
        /// <param name="entryB">The second entry to compare.</param>
        /// <returns>True is a difference was detected between the two entries, false otherwise</returns>
        public static bool BuildDiffReport(SchemaContext context, StringBuilder diffReport, DataEntry entryA, DataEntry entryB)
        {
            var currentAttributes = entryA.ToDictionary();
            var templateAttributes = entryB.ToDictionary();

            // Removed (exist in current, not in template)
            var removed = currentAttributes.Keys.Except(templateAttributes.Keys).OrderBy(k => k).ToList();
            if (removed.Count > 0)
            {
                diffReport.AppendLine("Removed attributes:");
                foreach (var name in removed)
                {
                    var a = currentAttributes[name];
                    diffReport.AppendLine($"\t- {a}");
                }
                diffReport.AppendLine();
            }

            // Added (exist in template, not in current)
            var added = templateAttributes.Keys.Except(currentAttributes.Keys).OrderBy(k => k).ToList();
            if (added.Count > 0)
            {
                diffReport.AppendLine("Added attributes:");
                foreach (var name in added)
                {
                    var b = templateAttributes[name];
                    diffReport.AppendLine($"\t+ {b}");
                }
                diffReport.AppendLine();
            }

            // Modified (exist in both by name but differ in fields)
            var common = currentAttributes.Keys.Intersect(templateAttributes.Keys).OrderBy(k => k);
            var anyModified = false;
            foreach (var name in common)
            {
                var a = currentAttributes[name];
                var b = templateAttributes[name];
                if (a.Equals(b))
                {
                    continue;
                }
                anyModified = true;
                diffReport.AppendLine($"Modified attribute: {name}");
                diffReport.AppendLine($"\t- {a}");
                diffReport.AppendLine($"\t+ {b}");
                diffReport.AppendLine();
            }

            return anyModified || removed.Any() || added.Any();
        }
    }
}