using UnityEditor;

namespace Schema.Unity.Editor
{
    public static class SchemaMenus
    {
        [MenuItem("Tools/Scheme Editor", false, 1)]
        public static void ShowEditorWindow()
        {
            EditorWindow.GetWindow<SchemaEditorWindow>("Scheme Editor");
        }

        [MenuItem("Tools/Scheme Debugger")]
        public static void ShowDebugWindow()
        {
            EditorWindow.GetWindow<SchemaDebugWindow>("Scheme Debugger");
        }

#if SCHEMA_DEBUG
        [MenuItem("Tools/Schema/Generate Test Data (50 entries)")]
        public static void GenerateTestData50()
        {
            VirtualScrollingTest.GenerateTestData(50);
        }
        [MenuItem("Tools/Schema/Generate Test Data (100 entries)")]
        public static void GenerateTestData100()
        {
            VirtualScrollingTest.GenerateTestData(100);
        }
        [MenuItem("Tools/Schema/Generate Test Data (500 entries)")]
        public static void GenerateTestData500()
        {
            VirtualScrollingTest.GenerateTestData(500);
        }
        [MenuItem("Tools/Schema/Generate Test Data (1,000 entries)")]
        public static void GenerateTestData1000()
        {
            VirtualScrollingTest.GenerateTestData(1_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (5,000 entries)")]
        public static void GenerateTestData5000()
        {
            VirtualScrollingTest.GenerateTestData(5_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (10,000 entries)")]
        public static void GenerateTestData10000()
        {
            VirtualScrollingTest.GenerateTestData(10_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (50,000 entries)")]
        public static void GenerateTestData50000()
        {
            VirtualScrollingTest.GenerateTestData(50_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (100,000 entries)")]
        public static void GenerateTestData100000()
        {
            VirtualScrollingTest.GenerateTestData(100_000);
        }
#endif
    }
}