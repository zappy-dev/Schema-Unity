using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Schema.Unity.Editor.Tests")]
// The optional feedback collector (Unity 2021.2+ only) reads SchemaEditorWindow, which is internal.
[assembly: InternalsVisibleTo("Schema.Unity.Editor.Feedback")]
[assembly: InternalsVisibleTo("Schema.Unity.Editor.Feedback.Tests")]
