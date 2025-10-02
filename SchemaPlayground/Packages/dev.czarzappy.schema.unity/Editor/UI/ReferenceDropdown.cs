using System;
using System.Linq;
using Schema.Core;
using Schema.Core.Data;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;

namespace Schema.Unity.Editor
{
    public static class ReferenceDropdown
    {
        /// <summary>
        /// Draws a dropdown for selecting a reference value from a referenced scheme's identifier values.
        /// </summary>
        /// <param name="label">Label to display next to the dropdown.</param>
        /// <param name="currentValue">The currently selected value.</param>
        /// <param name="refType">The ReferenceDataType describing the reference.</param>
        /// <param name="onNewValue">Callback when a dropdown option is selected</param>
        /// <param name="width">Optional width for the dropdown button.</param>
        /// <param name="style">Optional GUIStyle for the dropdown button.</param>
        /// <returns>The newly selected value if changed, otherwise the current value.</returns>
        public static void Draw(SchemaContext ctx, string label, object currentValue, ReferenceDataType refType, Action<object> onNewValue, float width = 0,
            GUIStyle style = null)
        {
            if (GetScheme(ctx, refType.ReferenceSchemeName).Try(out var refScheme))
            {
                var values = refScheme.GetIdentifierValues().Select(v => v?.ToString() ?? "").ToList();
                string displayValue = currentValue?.ToString() ?? "";
                if (string.IsNullOrEmpty(displayValue) && values.Count > 0)
                    displayValue = values[0];

                if (!string.IsNullOrEmpty(label))
                    EditorGUILayout.PrefixLabel(label);
                float buttonWidth = width > 0 ? width : 150f;
                if (SchemaLayout.DropdownButton(displayValue, buttonWidth, style))
                {
                    var menu = new GenericMenu();
                    foreach (var value in values)
                    {
                        menu.AddItem(new GUIContent(value), value == displayValue, () =>
                        {
                            onNewValue(value);
                        });
                    }
                    menu.ShowAsContext();
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Reference scheme '{refType.ReferenceSchemeName}' not found.", MessageType.Warning);
            }
        }
    }
} 