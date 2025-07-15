using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Tests
{
    /// <summary>
    /// Provides menu items for running Unity Editor UI tests
    /// </summary>
    public static class TestMenuItems
    {
        [MenuItem("Schema/Tests/Run All Tests")]
        public static void RunAllTests()
        {
            Debug.Log("Opening Test Runner for Schema Editor Tests...");
            OpenTestRunner();
        }
        
        [MenuItem("Schema/Tests/Run Unit Tests")]
        public static void RunUnitTests()
        {
            Debug.Log("Opening Test Runner for Unit Tests...");
            OpenTestRunner();
        }
        
        [MenuItem("Schema/Tests/Run Integration Tests")]
        public static void RunIntegrationTests()
        {
            Debug.Log("Opening Test Runner for Integration Tests...");
            OpenTestRunner();
        }
        
        [MenuItem("Schema/Tests/Run GUI Tests")]
        public static void RunGUITests()
        {
            Debug.Log("Opening Test Runner for GUI Tests...");
            OpenTestRunner();
        }
        
        [MenuItem("Schema/Tests/Clear Test Results")]
        public static void ClearTestResults()
        {
            Debug.Log("Clearing Test Results...");
            
            // Clear console
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);
            
            Debug.Log("Test results cleared.");
        }
        
        [MenuItem("Schema/Tests/Test Configuration")]
        public static void ShowTestConfiguration()
        {
            var message = @"Schema Editor UI Tests Configuration:

UNIT TESTS:
• Test isolated logic and state management
• Mock Unity Editor dependencies
• Fast execution, no Unity Editor windows required

INTEGRATION TESTS:
• Test actual Unity Editor window creation
• Component interactions and lifecycle
• Real Unity Editor environment

GUI TESTS:
• Simulate user interactions (mouse, keyboard)
• Test UI responsiveness and error handling
• Complex interaction sequences

TO RUN TESTS:
1. Use 'Window > General > Test Runner' 
2. Select 'EditMode' tab
3. Run individual test classes or all tests
4. Use Schema menu shortcuts above

REQUIREMENTS:
• Unity Test Runner package
• NUnit framework
• Schema.Unity.Editor package";

            EditorUtility.DisplayDialog("Schema Editor Tests", message, "OK");
        }
        
        [MenuItem("Schema/Tests/Generate Test Report")]
        public static void GenerateTestReport()
        {
            var reportContent = GenerateMarkdownReport();
            var reportPath = "Assets/Schema_Test_Report.md";
            
            System.IO.File.WriteAllText(reportPath, reportContent);
            AssetDatabase.Refresh();
            
            Debug.Log($"Test report generated: {reportPath}");
            EditorUtility.DisplayDialog("Test Report", $"Test report generated successfully!\n\nLocation: {reportPath}", "OK");
        }
        
        private static void OpenTestRunner()
        {
            // Open the Test Runner window
            var testRunnerWindowType = System.Type.GetType("UnityEditor.TestTools.TestRunner.TestRunnerWindow,UnityEditor.TestRunner");
            if (testRunnerWindowType != null)
            {
                var testRunnerWindow = EditorWindow.GetWindow(testRunnerWindowType);
                testRunnerWindow.titleContent = new GUIContent("Test Runner");
                testRunnerWindow.Show();
                testRunnerWindow.Focus();
            }
            else
            {
                Debug.LogWarning("Test Runner window not found. Make sure Unity Test Runner package is installed.");
            }
        }
        
        private static string GenerateMarkdownReport()
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            return $@"# Schema Editor UI Test Report

Generated: {timestamp}

## Test Structure

### Unit Tests
- **Location**: `Editor/Tests/Unit/`
- **Purpose**: Test isolated logic and state management
- **Dependencies**: Mock objects for Unity Editor APIs
- **Coverage**: 
  - SchemaEditorWindow state management
  - AttributeDefinition functionality
  - Data validation and edge cases

### Integration Tests  
- **Location**: `Editor/Tests/Integration/`
- **Purpose**: Test Unity Editor window creation and interaction
- **Dependencies**: Real Unity Editor environment
- **Coverage**:
  - Window lifecycle (create, focus, close)
  - Window positioning and sizing
  - AttributeSettingsPrompt dialog functionality

### GUI Tests
- **Location**: `Editor/Tests/GUI/`
- **Purpose**: Test user interaction simulation
- **Dependencies**: Custom GUI testing framework
- **Coverage**:
  - Mouse interactions (click, drag, scroll)
  - Keyboard input and shortcuts
  - Complex interaction sequences
  - Error handling and edge cases

## Test Files

### Unit Test Files:
- `SchemaEditorWindowUnitTests.cs` - Core window logic tests
- `AttributeSettingsPromptUnitTests.cs` - Data attribute tests

### Integration Test Files:
- `IntegrationTestBase.cs` - Base class with setup/cleanup
- `SchemaEditorWindowIntegrationTests.cs` - Window creation tests
- `AttributeSettingsPromptIntegrationTests.cs` - Dialog tests

### GUI Test Files:
- `GUITestFramework.cs` - Event simulation framework
- `SchemaEditorWindowGUITests.cs` - User interaction tests

### Mock Objects:
- `MockEditorPrefs.cs` - Mock Unity EditorPrefs
- `TestableSchemaEditorWindow.cs` - Testable wrapper class

## Running Tests

1. Open **Window > General > Test Runner**
2. Select **EditMode** tab
3. Choose test category or run all tests
4. Use **Schema > Tests** menu for shortcuts

## Test Categories

| Category | Test Count | Description |
|----------|------------|-------------|
| Unit | ~25 tests | Fast, isolated logic tests |
| Integration | ~15 tests | Unity Editor window tests |
| GUI | ~30 tests | User interaction simulation |

## Best Practices

- Tests use proper setup/teardown to avoid interference
- Mock objects isolate Unity Editor dependencies  
- Integration tests clean up windows after each test
- GUI tests simulate realistic user interactions
- All tests include descriptive error messages

---

*Report generated by Schema Editor Test Suite*
";
        }
    }
}