using UnityEditor;

namespace Schema.Unity.Editor
{
    public static class SchemaMenus
    {
        [MenuItem("Tools/Scheme Editor")]
        public static void ShowEditorWindow()
        {
            EditorWindow.GetWindow<SchemaEditorWindow>("Scheme Editor");
        }

#if SCHEMA_DEBUG
        [MenuItem("Tools/Scheme Debugger")]
        public static void ShowDebugWindow()
        {
            EditorWindow.GetWindow<SchemaDebugWindow>("Scheme Debugger");
        }
#endif
    }
}