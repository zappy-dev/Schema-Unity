using System.Linq;
using Schema.Core.Data;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;

namespace Schema.Unity.Editor
{
    public class AttributeSettingsPrompt : EditorWindow
    {
        private DataScheme scheme;
        private AttributeDefinition attribute; // Default column width
        private AttributeDefinition editAttribute;

        public static void ShowWindow(DataScheme scheme, AttributeDefinition attribute)
        {
            // Show a new instance of the prompt window
            var prompt = GetWindow<AttributeSettingsPrompt>(utility: true, $"Column Settings - {attribute.AttributeName}");
            prompt.Initialize(scheme, attribute);
            prompt.ShowUtility(); // Use over ModelUtility to prevent UI issues with GenericMenu popup
        }

        private void Initialize(DataScheme scheme, AttributeDefinition attribute)
        {
            this.scheme = scheme;
            this.attribute = attribute;
            this.editAttribute = attribute.Clone() as AttributeDefinition;
        }

        private void OnGUI()
        {
            // Text field for entering the width
            editAttribute.AttributeName = EditorGUILayout.TextField("Attribute Name", editAttribute.AttributeName);
            
            EditorGUILayout.LabelField("Attribute Tooltip");
            editAttribute.AttributeToolTip = EditorGUILayout.TextArea(editAttribute.AttributeToolTip);

            // Support Reference DataType default value selection
            if (editAttribute.DataType is ReferenceDataType refType)
            {
                ReferenceDropdown.Draw("Default Value", editAttribute.DefaultValue, refType, newValue =>
                {
                    editAttribute.DefaultValue = newValue;
                });
            }
            else
            {
                editAttribute.DefaultValue = EditorGUILayout.TextField("Default Value", editAttribute.DefaultValue?.ToString() ?? "");
            }

            var identifierLabel = new GUIContent("Is Identifier?",
                "Only one attribute is allowed to be an identifier per Schema. Identifier Attributes must also be unique.");
            using (var identifierCheck = new EditorGUI.ChangeCheckScope())
            {
                var setIsIdentifier = EditorGUILayout.Toggle(identifierLabel, editAttribute.IsIdentifier);

                if (identifierCheck.changed)
                {
                    if (setIsIdentifier)
                    {
                        // validate that this attribute can be converted to an identifier
                        if (scheme.GetIdentifierAttribute().Try(out var identifierAttribute))
                        {
                            EditorUtility.DisplayDialog("Schema", $"Schema {scheme} already contains an identifier attribute: {identifierAttribute}", "OK");
                        }
                        else if (scheme.GetValuesForAttribute(attribute)
                                 .GroupBy(v => v)
                                 .Any(g => g.Count() > 1))
                        {
                            EditorUtility.DisplayDialog("Schema", $"Attribute contains duplicate entries", "Ok");
                        }
                        else
                        {
                            editAttribute.IsIdentifier = true;
                        }
                    }
                    else
                    {
                        // TODO: validate that no other scheme is referencing this identifier
                        editAttribute.IsIdentifier = false;
                    }
                }
            }
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("GUI Settings");
            editAttribute.ColumnWidth = EditorGUILayout.IntField("Column Width", editAttribute.ColumnWidth);

            // Apply button to set the column width
            if (GUILayout.Button("Update Settings"))
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            scheme.UpdateAttributeName(attribute.AttributeName, editAttribute.AttributeName);
            attribute.Copy(editAttribute);

            // Updating an attribute name can impact referencing schemes, save all dirty
            Save();
            
            Close();
        }
    }
}