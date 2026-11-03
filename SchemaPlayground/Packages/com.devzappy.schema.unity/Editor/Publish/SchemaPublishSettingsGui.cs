using System.Linq;
using Schema.Core;
using Schema.Core.Publish;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// The project's publish settings, under Project Settings > Schema Publish: what Publish writes, and which of
    /// the project's own publish steps it will run.
    /// </summary>
    internal static class SchemaPublishSettingsGui
    {
        private static readonly SchemaContext Context = new SchemaContext
        {
            Driver = "User_Edit_Publish_Settings",
        };

        private static SchemaPublishSettings settings;
        private static string loadError;

        private static void Reload()
        {
            var loadRes = SchemaPublishSettingsStore.Load(Context);
            if (loadRes.Try(out var loaded))
            {
                settings = loaded;
                loadError = null;
            }
            else
            {
                settings = SchemaPublishSettings.Defaults();
                loadError = loadRes.Message;
            }
        }

        private static void Draw()
        {
            if (settings == null) Reload();

            if (!string.IsNullOrEmpty(loadError))
            {
                EditorGUILayout.HelpBox(loadError, MessageType.Error);
            }

            EditorGUILayout.LabelField("What Publish writes", EditorStyles.boldLabel);

            var resources = EditorGUILayout.Toggle(
                new GUIContent("Resources JSON", "Write the published table to the scheme's publish target: for the default RESOURCES target, the filtered JSON under Assets/Plugins/Schema/Resources that the runtime loads. Switch off when the project publishes the data itself."),
                settings.PublishResourcesJson);
            var csharp = EditorGUILayout.Toggle(
                new GUIContent("Generated C#", "Generate the strongly-typed wrappers for the scheme into the Manifest's CSharpExportPath. Switch off when the project does not use the generated code."),
                settings.GenerateCSharp);

            if (resources != settings.PublishResourcesJson || csharp != settings.GenerateCSharp)
            {
                settings.PublishResourcesJson = resources;
                settings.GenerateCSharp = csharp;
                var saveRes = SchemaPublishSettingsStore.Save(Context, settings);
                loadError = saveRes.Failed ? saveRes.Message : null;
            }

            EditorGUILayout.LabelField($"Kept in {SchemaPublishSettingsStore.SettingsPath}", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("This project's publish steps", EditorStyles.boldLabel);

            var steps = SchemaPublishStepRegistry.Steps;
            if (steps.Count == 0)
            {
                EditorGUILayout.LabelField(
                    $"None. A project adds one by writing a class that implements {nameof(ISchemaPublishStep)} (or derives from {nameof(SchemaPublishStep)}); it is found and run on the next publish.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                foreach (var step in steps.OrderBy(s => s.Order))
                {
                    EditorGUILayout.LabelField($"- {step.GetType().FullName} (order {step.Order})", EditorStyles.miniLabel);
                }
            }

            if (settings.IsEveryBuiltInOutputOff && steps.Count == 0)
            {
                EditorGUILayout.HelpBox("Publish would do nothing: both built-in outputs are off and this project has no publish steps.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                {
                    SchemaPublishStepRegistry.Invalidate();
                    Reload();
                }
            }
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider() => new SettingsProvider("Project/Schema Publish", SettingsScope.Project)
        {
            label = "Schema Publish",
            keywords = new[] { "schema", "publish", "resources", "codegen", "bake" },
            activateHandler = (_, __) => Reload(),
            guiHandler = _ => Draw(),
        };
    }
}
