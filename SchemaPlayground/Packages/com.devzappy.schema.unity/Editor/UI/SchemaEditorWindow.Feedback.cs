using System;
using Schema.Core;
using static Schema.Core.Schema;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// The seam the optional feedback collector (<c>Editor/Feedback</c>, Unity 2021.2+) plugs into. This
    /// assembly knows nothing about that one; it only offers a hook to draw into the toolbar and a few read-only
    /// questions about what the person has open. Nothing here runs unless the feedback assembly is compiled in.
    /// </summary>
    internal partial class SchemaEditorWindow
    {
        /// <summary>Drawn at the end of the toolbar row (next to Save All and Publish All). Set by the feedback assembly.</summary>
        internal static Action<SchemaEditorWindow> DrawToolbarExtras;

        /// <summary>The scheme open in the table, or empty.</summary>
        internal string FeedbackSelectedSchemeName => selectedSchemeName ?? string.Empty;

        /// <summary>The cell that last had focus in the table: its column, row index and current value.</summary>
        internal bool TryGetFeedbackCell(out string attribute, out int row, out string value)
        {
            attribute = null;
            row = -1;
            value = null;
            try
            {
                if (!lastFocusedCell.IsSet) return false;

                var ctx = new SchemaContext { Driver = "Feedback_Cell" };
                if (!GetEntryDataForTableCell(ctx, lastFocusedCell).Try(out var entryData)) return false;

                var (entry, definition) = entryData;
                attribute = definition.AttributeName;
                row = GetGlobalPosition(lastFocusedCell).globalEntryIdx;
                var data = entry.GetDataDirect(definition);
                value = data?.ToString();
                if (value != null && value.Length > 200) value = value.Substring(0, 200) + "...";
                return true;
            }
            catch (Exception)
            {
                // a cell that cannot be described is no reason to fail a bug report
                return false;
            }
        }

        /// <summary>The open scheme as the JSON that Save writes; null when none is open or it cannot be serialised.</summary>
        internal string SerializeSelectedSchemeForFeedback()
        {
            try
            {
                var ctx = new SchemaContext { Driver = "Feedback_Scheme" };
                if (!GetSelectedScheme(ctx).Try(out var scheme)) return null;
                if (!GetStorage(ctx).Try(out var storage)) return null;
                return storage.JsonSchemeStorageFormat.Serialize(ctx, scheme).Try(out var json) ? json : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
