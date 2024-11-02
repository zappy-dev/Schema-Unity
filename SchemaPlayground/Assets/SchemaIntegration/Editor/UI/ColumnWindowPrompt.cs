using System;
using Schema.Core;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public class AttributeSettingsPrompt : EditorWindow
    {
        private DataScheme scheme;
        private AttributeDefinition attribute; // Default column width
        private string attributeName;
        private string attributeTooltip;
        private int columnWidth;

        public static void ShowWindow(DataScheme schema, AttributeDefinition attribute)
        {
            // Show a new instance of the prompt window
            var prompt = GetWindow<AttributeSettingsPrompt>(utility: true, $"Column Settings - {attribute.AttributeName}");
            prompt.Initialize(schema, attribute);
            prompt.ShowModalUtility();
        }

        private void Initialize(DataScheme scheme, AttributeDefinition attribute)
        {
            this.scheme = scheme;
            this.attribute = attribute;
            this.attributeName = attribute.AttributeName;
            this.attributeTooltip = attribute.AttributeToolTip ?? String.Empty;
            this.columnWidth = attribute.ColumnWidth;
        }

        private void OnGUI()
        {
            // Text field for entering the width
            attributeName = EditorGUILayout.TextField("Attribute Name", attributeName);
            
            EditorGUILayout.LabelField("Attribute Tooltip");
            attributeTooltip = EditorGUILayout.TextArea(attributeTooltip);
            
            EditorGUILayout.Separator();
            
            columnWidth = EditorGUILayout.IntField("Column Width:", columnWidth);

            // Apply button to set the column width
            if (GUILayout.Button("Update Settings"))
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            attribute.ColumnWidth = columnWidth;
            attribute.AttributeToolTip = attributeTooltip;
            scheme.UpdateAttributeName(attribute.AttributeName, attributeName);
            
            Close();
        }
    }
}