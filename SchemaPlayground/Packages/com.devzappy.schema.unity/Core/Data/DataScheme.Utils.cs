using System;
using System.Linq;
using System.Text;

namespace Schema.Core.Data
{
    public partial class DataScheme
    {
        /// <summary>
        /// Builds a diff report between two schemes.
        /// </summary>
        /// <param name="context">The context to use for the diff report.</param>
        /// <param name="diffReport">The diff report to build.</param>
        /// <param name="schemeA">The first scheme to compare.</param>
        /// <param name="schemeB">The second scheme to compare.</param>
        /// <returns>True is a difference was detected between the two schemes, false otherwise</returns>
        public static bool BuildAttributeDiffReport(SchemaContext context, StringBuilder diffReport, DataScheme schemeA, DataScheme schemeB)
        {
            var currentAttributes = schemeA.GetAttributes().ToDictionary(a => a.AttributeName);
            var templateAttributes = schemeB.GetAttributes().ToDictionary(a => a.AttributeName);
            
            // Removed (exist in current, not in template)
            var removed = currentAttributes.Keys.Except(templateAttributes.Keys).OrderBy(k => k).ToList();
            if (removed.Count > 0)
            {
                diffReport.AppendLine("Removed attributes:");
                foreach (var name in removed)
                {
                    var a = currentAttributes[name];
                    diffReport.AppendLine($"\t- {a.AttributeName} ({a.DataType.TypeName})");
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
                    diffReport.AppendLine($"\t+ {b.AttributeName} ({b.DataType.TypeName})");
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
            
                if (!a.DataType.Equals(b.DataType))
                {
                    // TODO: More in-depth attribute diff report?
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.DataType)}: {a.DataType?.TypeName} -> {b.DataType?.TypeName}");
                }
            
                if (a.IsIdentifier != b.IsIdentifier)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.IsIdentifier)}: {a.IsIdentifier} -> {b.IsIdentifier}");
                }
            
                if (a.ShouldPublish != b.ShouldPublish)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.ShouldPublish)}: {a.ShouldPublish} -> {b.ShouldPublish}");
                }
            
                // UI fields (informational)
                if (a.AttributeToolTip != b.AttributeToolTip)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.AttributeToolTip)}: '{a.AttributeToolTip}' -> '{b.AttributeToolTip}'");
                }
            
                if (a.ColumnWidth != b.ColumnWidth)
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.ColumnWidth)}: {a.ColumnWidth} -> {b.ColumnWidth}");
                }
            
                // Default value comparison (best-effort string rep)
                var aDefault = a.DefaultValue?.ToString();
                var bDefault = b.DefaultValue?.ToString();
                if (!string.Equals(aDefault, bDefault, StringComparison.Ordinal))
                {
                    diffReport.AppendLine($"\t{nameof(AttributeDefinition.DefaultValue)}: '{aDefault}' -> '{bDefault}'");
                }
            
                // Reference data type details if applicable
                if (a.DataType is ReferenceDataType ar && b.DataType is ReferenceDataType br)
                {
                    if (ar.ReferenceSchemeName != br.ReferenceSchemeName)
                    {
                        diffReport.AppendLine($"\tReference Scheme: {ar.ReferenceSchemeName} -> {br.ReferenceSchemeName}");
                    }
                    if (ar.ReferenceAttributeName != br.ReferenceAttributeName)
                    {
                        diffReport.AppendLine($"\tReference Attribute: {ar.ReferenceAttributeName} -> {br.ReferenceAttributeName}");
                    }
                    if (ar.SupportsEmptyReferences != br.SupportsEmptyReferences)
                    {
                        diffReport.AppendLine($"\tAllow Empty Refs: {ar.SupportsEmptyReferences} -> {br.SupportsEmptyReferences}");
                    }
                }
            
                diffReport.AppendLine();
            }

            return anyModified || removed.Any() || added.Any();
        }
        
    }
}