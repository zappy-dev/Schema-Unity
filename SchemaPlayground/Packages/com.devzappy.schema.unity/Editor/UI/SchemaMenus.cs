using UnityEditor;

namespace Schema.Unity.Editor
{
    public static class SchemaMenus
    {
        [MenuItem("Tools/Schema Editor", false, 1)]
        public static void ShowEditorWindow()
        {
            EditorWindow.GetWindow<SchemaEditorWindow>("Schema Editor");
        }

        [MenuItem("Tools/Schema Debugger")]
        public static void ShowDebugWindow()
        {
            EditorWindow.GetWindow<SchemaDebugWindow>("Schema Debugger");
        }

#if SCHEMA_PERF || SCHEMA_DEBUG
        [MenuItem("Tools/Schema/Generate Test Data (50 entries)")]
        public static void GenerateTestData50()
        {
            _ = VirtualScrollingTest.GenerateTestData(50);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (100 entries)")]
        public static void GenerateTestData100()
        {
            _ = VirtualScrollingTest.GenerateTestData(100);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (500 entries)")]
        public static void GenerateTestData500()
        {
            _ = VirtualScrollingTest.GenerateTestData(500);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (1,000 entries)")]
        public static void GenerateTestData1000()
        {
            _ = VirtualScrollingTest.GenerateTestData(1_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (5,000 entries)")]
        public static void GenerateTestData5000()
        {
            _ = VirtualScrollingTest.GenerateTestData(5_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (10,000 entries)")]
        public static void GenerateTestData10000()
        {
            _ = VirtualScrollingTest.GenerateTestData(10_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (50,000 entries)")]
        public static void GenerateTestData50000()
        {
            _ = VirtualScrollingTest.GenerateTestData(50_000);
        }
        
        [MenuItem("Tools/Schema/Generate Test Data (100,000 entries)")]
        public static void GenerateTestData100000()
        {
            _ = VirtualScrollingTest.GenerateTestData(100_000);
        }
#endif
    }
}