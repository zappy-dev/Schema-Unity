using System;
using Schema.Core;
using Schema.Core.Data;
using Schema.Unity.Editor.Ext;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;
using static Schema.Core.SchemaResult;
using static Schema.Unity.Editor.LayoutUtils;

namespace Schema.Unity.Editor
{
    public class CreateNewSchemeWizardWindow : EditorWindow
    {
        [NonSerialized]
        private string newSchemeName;

        [NonSerialized] private string createErrorMessage;
        
        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(createErrorMessage))
            {
                EditorGUILayout.HelpBox(createErrorMessage, MessageType.Error);
            }
            
            GUI.SetNextControlName(SchemaEditorWindow.CONTROL_NAME_NEW_SCHEME_NAME_FIELD);
            newSchemeName = EditorGUILayout.TextField( "Name of new Scheme",newSchemeName);

            // TODO: Sanitized input
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newSchemeName)))
            {
                if (GUILayout.Button("Create"))
                {
                    var createStartRes = CreateNewScheme(SchemaContext.EditContext.WithDriver("User_Create_New_Schema"), newSchemeName);

                    if (createStartRes.Failed)
                    {
                        createErrorMessage = createStartRes.Message;
                    }
                    else
                    {
                        // close the wizard and got back to the SchemaEditorWindow
                        Close();
                        GetWindow<SchemaEditorWindow>().Focus();
                    }
                }
            }
        }
        
        private SchemaResult CreateNewScheme(SchemaContext context, string newSchemeName)
        {
            var newSchema = new DataScheme(newSchemeName);
            context.Scheme = newSchema;
            
            // Initialize with some starting data
            var newAttrRes = newSchema.AddAttribute(context, "ID", DataType.Text, defaultValue: string.Empty, isIdentifier: true);
            if (!newAttrRes.Try(out var newIdAttr))
            {
                return Fail(context, newAttrRes.Message);
            }

            var dataEntry = new DataEntry();
            dataEntry.Add(newIdAttr.AttributeName, string.Empty, context);
            var addEntryRes = newSchema.AddEntry(context, dataEntry);

            if (addEntryRes.Failed)
            {
                return Fail(context, newAttrRes.Message);
            }

            if (!GetStorage(context).Try(out var storage, out var storageErr)) return storageErr.Cast();
                            
            // Create a relative path for the new schema file
            string fileName = $"{newSchemeName}.{storage.DefaultSchemeStorageFormat.Extension}";
            string relativePath = $"{DefaultContentDirectory}/{fileName}";
            // string relativePath = fileName; // Default to just the filename (relative to Content folder)
                            
            GetWindow<SchemaEditorWindow>().SubmitAddSchemeRequest(context, newSchema, importFilePath: relativePath).FireAndForget();
            
            return Pass($"Created new Scheme: {newSchemeName}", context);
        }
    }
}