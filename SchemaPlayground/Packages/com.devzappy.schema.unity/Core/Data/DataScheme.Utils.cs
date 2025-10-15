using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Schema.Core.Logging;

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

        /// <summary>
        /// Sort the given set of Data Schemes in topological order (first schemes should have no references on future schemes, later schemes can depend on earlier schemes but not schemes after them)
        /// </summary>
        /// <param name="schemes">Schemes to sort</param>
        /// <returns></returns>
        public static SchemaResult<IEnumerable<DataScheme>> TopologicalSortByReferences(SchemaContext context, IEnumerable<DataScheme> schemes)
        {
            var res = SchemaResult<IEnumerable<DataScheme>>.New(context);
            var schemesToProcess = schemes.ToList();
            int totalSchemesToProcess = schemesToProcess.Count;

            // First pass, process schemes invalid schemes, schemes with no references
            var result = new List<DataScheme>();
            for (int i = schemesToProcess.Count - 1; i >= 0; i--)
            {
                var current = schemesToProcess[i];
                if (current == null)
                {
                    schemesToProcess.RemoveAt(i);
                    continue;
                }

                // Immediately add schemes with no reference attributes
                if (!current.GetReferenceAttributes().Any())
                {
                    result.Add(current);
                    schemesToProcess.RemoveAt(i);
                }
            }
            
            // second pass, process schemes with references
            // check if all referenced schemes exist earlier in results
            int index = 0;
            int lastIterationSize = schemesToProcess.Count;
            while (schemesToProcess.Count > 0)
            {
                var current = schemesToProcess[index];

                if (current.GetReferenceAttributes()
                    .Select(refAttr => refAttr.DataType as ReferenceDataType)
                    .All(refDataType =>
                    {
                        var resultsContainsRef = result.Select(s => s.SchemeName).Contains(refDataType.ReferenceSchemeName);
                        var isSelfRef = refDataType.ReferenceSchemeName == current.SchemeName;
                        return isSelfRef || resultsContainsRef;
                    }))
                {
                    result.Add(current);
                    schemesToProcess.RemoveAt(index);
                }

                if (schemesToProcess.Count > 0)
                {
                    index = (index + 1) % schemesToProcess.Count;
                    if (index == 0)
                    {
                        if (lastIterationSize == schemesToProcess.Count)
                        {
                            var schemeReport = string.Join(",", schemesToProcess.Select(s => s.SchemeName));
                            res.Fail($"Unable to topological sort the following schemes, check for cyclical dependencies: {schemeReport}");
                            // No schemes were moved, meaning there is a cyclical dependency
                            break;
                        }
                        lastIterationSize = schemesToProcess.Count;
                    }
                }
            }

            return res.Pass(result);
        }
    }
}