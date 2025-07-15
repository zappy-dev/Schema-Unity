# Unity Editor UI Testing Guide for SchemaPlayground

## Overview

This guide provides comprehensive approaches for testing Unity Editor UI components in the SchemaPlayground project. The project contains several Editor UI components that need testing, including:

- **SchemaEditorWindow**: Main editor window with table view and explorer functionality
- **AttributeSettingsPrompt**: Modal dialog for column settings
- **EditorProgressReporter**: Progress reporting UI component
- **SchemaLayout**: Layout utilities for UI components

## Project Structure Analysis

```
SchemaPlayground/
├── Packages/
│   └── dev.czarzappy.schema.unity/
│       ├── Core/              # Core functionality
│       └── Editor/            # Editor-specific code
│           ├── UI/            # UI components to test
│           │   ├── SchemaEditorWindow.cs (1109 lines)
│           │   ├── AttributeSettingsPrompt.cs 
│           │   ├── EditorProgressReporter.cs
│           │   └── SchemaLayout.cs
│           └── Ext/           # Extension methods
```

## Testing Approaches

### 1. Unit Testing with Mocking

#### Setup Test Infrastructure

First, create a test assembly definition in the Editor directory:

```json
// Editor/Tests/Schema.Unity.Editor.Tests.asmdef
{
    "name": "Schema.Unity.Editor.Tests",
    "rootNamespace": "Schema.Unity.Editor.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Schema.Unity.Editor",
        "Schema.Core"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ]
}
```

#### Mock Unity Editor APIs

Create mock classes for Unity Editor components:

```csharp
// Editor/Tests/Mocks/MockEditorWindow.cs
public class MockEditorWindow : EditorWindow
{
    public List<string> GuiCalls = new List<string>();
    public bool IsRepaintCalled = false;
    
    public new void Repaint()
    {
        IsRepaintCalled = true;
        GuiCalls.Add("Repaint");
    }
}

// Editor/Tests/Mocks/MockGUIUtility.cs
public static class MockGUIUtility
{
    public static Dictionary<string, object> StoredPrefs = new Dictionary<string, object>();
    
    public static void SetString(string key, string value)
    {
        StoredPrefs[key] = value;
    }
    
    public static string GetString(string key, string defaultValue = "")
    {
        return StoredPrefs.ContainsKey(key) ? (string)StoredPrefs[key] : defaultValue;
    }
}
```

#### Test Schema Editor Window Logic

```csharp
// Editor/Tests/UI/SchemaEditorWindowTests.cs
[TestFixture]
public class SchemaEditorWindowTests
{
    private MockEditorWindow mockWindow;
    private TestableSchemaEditorWindow testWindow;
    
    [SetUp]
    public void Setup()
    {
        mockWindow = new MockEditorWindow();
        testWindow = new TestableSchemaEditorWindow();
    }
    
    [Test]
    public void TestManifestLoadResponse_SetsCorrectly()
    {
        // Arrange
        var loadResponse = SchemaResult<ManifestLoadStatus>.Success(ManifestLoadStatus.Success);
        
        // Act
        testWindow.LatestManifestLoadResponse = loadResponse;
        
        // Assert
        Assert.That(testWindow.LatestManifestLoadResponse.Status, Is.EqualTo(RequestStatus.Success));
        Assert.That(testWindow.LatestManifestLoadResponse.Passed, Is.True);
    }
    
    [Test]
    public void TestResponseHistory_AddsCorrectly()
    {
        // Arrange
        var response1 = SchemaResult.Success("Test 1");
        var response2 = SchemaResult.Success("Test 2");
        
        // Act
        testWindow.AddToResponseHistory(response1);
        testWindow.AddToResponseHistory(response2);
        
        // Assert
        Assert.That(testWindow.GetResponseHistoryCount(), Is.EqualTo(2));
        Assert.That(testWindow.GetLatestResponse().Message, Is.EqualTo("Test 2"));
    }
    
    [Test]
    public void TestSchemaSelection_UpdatesEditorPrefs()
    {
        // Arrange
        var schemaName = "TestSchema";
        MockGUIUtility.StoredPrefs.Clear();
        
        // Act
        testWindow.SelectScheme(schemaName);
        
        // Assert
        Assert.That(MockGUIUtility.GetString("Schema:SelectedSchemeName"), Is.EqualTo(schemaName));
    }
}

// Testable wrapper class to expose internal methods
public class TestableSchemaEditorWindow : SchemaEditorWindow
{
    public void AddToResponseHistory(SchemaResult result)
    {
        responseHistory.Add(result);
    }
    
    public int GetResponseHistoryCount()
    {
        return responseHistory.Count;
    }
    
    public SchemaResult GetLatestResponse()
    {
        return responseHistory.LastOrDefault();
    }
    
    public void SelectScheme(string schemeName)
    {
        selectedSchemeName = schemeName;
        EditorPrefs.SetString(EDITORPREFS_KEY_SELECTEDSCHEME, schemeName);
    }
}
```

### 2. Integration Testing with Unity Test Runner

#### Test Editor Window Creation and State

```csharp
// Editor/Tests/Integration/EditorWindowIntegrationTests.cs
[TestFixture]
public class EditorWindowIntegrationTests
{
    private SchemaEditorWindow window;
    
    [SetUp]
    public void Setup()
    {
        // Clean up any existing windows
        var existingWindows = Resources.FindObjectsOfTypeAll<SchemaEditorWindow>();
        foreach (var w in existingWindows)
        {
            w.Close();
        }
    }
    
    [TearDown]
    public void TearDown()
    {
        if (window != null)
        {
            window.Close();
        }
    }
    
    [Test]
    public void TestEditorWindowCreation()
    {
        // Act
        window = EditorWindow.GetWindow<SchemaEditorWindow>();
        
        // Assert
        Assert.That(window, Is.Not.Null);
        Assert.That(window.titleContent.text, Is.EqualTo("Schema Editor"));
    }
    
    [Test]
    public void TestWindowPersistence()
    {
        // Arrange
        window = EditorWindow.GetWindow<SchemaEditorWindow>();
        var windowId = window.GetInstanceID();
        
        // Act
        // Simulate Unity session restart
        window.Close();
        window = EditorWindow.GetWindow<SchemaEditorWindow>();
        
        // Assert
        Assert.That(window, Is.Not.Null);
        // Window should be recreated with same settings
    }
}
```

#### Test UI Component Interactions

```csharp
// Editor/Tests/Integration/UIComponentTests.cs
[TestFixture]
public class UIComponentTests
{
    [Test]
    public void TestAttributeSettingsPrompt_ShowsCorrectly()
    {
        // Arrange
        var scheme = new DataScheme("TestScheme");
        var attribute = new AttributeDefinition("TestAttribute", DataType.Text);
        
        // Act
        AttributeSettingsPrompt.ShowWindow(scheme, attribute);
        
        // Assert
        var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
        Assert.That(windows.Length, Is.EqualTo(1));
        
        var window = windows[0];
        Assert.That(window.titleContent.text, Does.Contain("TestAttribute"));
        
        // Cleanup
        window.Close();
    }
    
    [Test]
    public void TestEditorProgressReporter_ReportsProgress()
    {
        // Arrange
        var reporter = new EditorProgressReporter();
        
        // Act
        reporter.ReportProgress(0.5f, "Test Progress");
        
        // Assert
        // Check that progress bar is displayed correctly
        // This would require GUI testing framework
    }
}
```

### 3. GUI Testing with Custom Framework

#### Create GUI Test Utilities

```csharp
// Editor/Tests/Utils/GUITestUtils.cs
public static class GUITestUtils
{
    public static void SimulateGUIEvent(EventType eventType, Vector2 mousePosition = default)
    {
        var e = new Event
        {
            type = eventType,
            mousePosition = mousePosition,
            button = 0
        };
        
        Event.current = e;
    }
    
    public static void SimulateMouseClick(Vector2 position)
    {
        SimulateGUIEvent(EventType.MouseDown, position);
        SimulateGUIEvent(EventType.MouseUp, position);
    }
    
    public static void SimulateKeyPress(KeyCode key)
    {
        var e = new Event
        {
            type = EventType.KeyDown,
            keyCode = key
        };
        
        Event.current = e;
    }
    
    public static Rect FindGUIRect(string controlName)
    {
        // Implementation would depend on your GUI layout
        // Could use reflection or custom control tracking
        return new Rect();
    }
}
```

#### Test GUI Interactions

```csharp
// Editor/Tests/GUI/SchemaEditorGUITests.cs
[TestFixture]
public class SchemaEditorGUITests
{
    private SchemaEditorWindow window;
    private Event testEvent;
    
    [SetUp]
    public void Setup()
    {
        window = EditorWindow.GetWindow<SchemaEditorWindow>();
        testEvent = new Event();
    }
    
    [TearDown]
    public void TearDown()
    {
        window?.Close();
    }
    
    [Test]
    public void TestTableView_RespondsToMouseClick()
    {
        // Arrange
        var clickPosition = new Vector2(100, 100);
        GUITestUtils.SimulateMouseClick(clickPosition);
        
        // Act
        window.Repaint();
        
        // Assert
        // Verify that the click was processed
        // This requires custom verification logic
    }
    
    [Test]
    public void TestKeyboardShortcuts()
    {
        // Arrange
        GUITestUtils.SimulateKeyPress(KeyCode.F5); // Refresh
        
        // Act
        window.Repaint();
        
        // Assert
        // Verify refresh action was triggered
    }
}
```

### 4. Automated Visual Testing

#### Screenshot Comparison Testing

```csharp
// Editor/Tests/Visual/VisualRegressionTests.cs
[TestFixture]
public class VisualRegressionTests
{
    private string baselineDirectory = "Assets/Tests/Baselines/";
    private string outputDirectory = "Assets/Tests/Output/";
    
    [Test]
    public void TestSchemaEditorWindow_VisualRegression()
    {
        // Arrange
        var window = EditorWindow.GetWindow<SchemaEditorWindow>();
        window.position = new Rect(0, 0, 800, 600);
        
        // Act
        window.Repaint();
        var screenshot = CaptureWindow(window);
        
        // Assert
        var baselineFile = Path.Combine(baselineDirectory, "SchemaEditorWindow_Baseline.png");
        var outputFile = Path.Combine(outputDirectory, "SchemaEditorWindow_Current.png");
        
        File.WriteAllBytes(outputFile, screenshot);
        
        if (File.Exists(baselineFile))
        {
            var baseline = File.ReadAllBytes(baselineFile);
            Assert.That(CompareImages(baseline, screenshot), Is.True, 
                "Visual regression detected in SchemaEditorWindow");
        }
        else
        {
            File.WriteAllBytes(baselineFile, screenshot);
            Assert.Inconclusive("Baseline created for SchemaEditorWindow");
        }
        
        // Cleanup
        window.Close();
    }
    
    private byte[] CaptureWindow(EditorWindow window)
    {
        // Implementation depends on Unity version
        // Could use ScreenCapture.CaptureScreenshot or custom texture capture
        return new byte[0]; // Placeholder
    }
    
    private bool CompareImages(byte[] image1, byte[] image2)
    {
        // Simple byte comparison or use image comparison library
        return image1.SequenceEqual(image2);
    }
}
```

### 5. Property-Based Testing

#### Test with Random Data

```csharp
// Editor/Tests/Property/PropertyBasedTests.cs
[TestFixture]
public class PropertyBasedTests
{
    private System.Random random = new System.Random();
    
    [Test]
    public void TestSchemaEditorWindow_HandlesRandomSchemas([Random(1, 100, 10)] int schemaCount)
    {
        // Arrange
        var window = EditorWindow.GetWindow<SchemaEditorWindow>();
        var schemas = GenerateRandomSchemas(schemaCount);
        
        // Act & Assert
        foreach (var schema in schemas)
        {
            Assert.DoesNotThrow(() => {
                window.LoadSchema(schema);
                window.Repaint();
            });
        }
        
        window.Close();
    }
    
    private List<DataScheme> GenerateRandomSchemas(int count)
    {
        var schemas = new List<DataScheme>();
        for (int i = 0; i < count; i++)
        {
            var schema = new DataScheme($"Schema_{i}");
            var attrCount = random.Next(1, 20);
            
            for (int j = 0; j < attrCount; j++)
            {
                var dataType = (DataType)random.Next(0, Enum.GetValues(typeof(DataType)).Length);
                schema.AddAttribute(new AttributeDefinition($"Attr_{j}", dataType));
            }
            
            schemas.Add(schema);
        }
        return schemas;
    }
}
```

### 6. Performance Testing

#### Test UI Performance

```csharp
// Editor/Tests/Performance/PerformanceTests.cs
[TestFixture]
public class PerformanceTests
{
    [Test]
    public void TestSchemaEditorWindow_PerformanceWithLargeDataset()
    {
        // Arrange
        var window = EditorWindow.GetWindow<SchemaEditorWindow>();
        var largeSchema = CreateLargeSchema(1000); // 1000 attributes
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        window.LoadSchema(largeSchema);
        window.Repaint();
        stopwatch.Stop();
        
        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), 
            "Schema loading should complete within 1 second");
        
        // Test scrolling performance
        stopwatch.Restart();
        for (int i = 0; i < 100; i++)
        {
            GUITestUtils.SimulateGUIEvent(EventType.ScrollWheel, new Vector2(0, -10));
            window.Repaint();
        }
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), 
            "Scrolling should be smooth");
        
        window.Close();
    }
    
    private DataScheme CreateLargeSchema(int attributeCount)
    {
        var schema = new DataScheme("LargeSchema");
        for (int i = 0; i < attributeCount; i++)
        {
            schema.AddAttribute(new AttributeDefinition($"Attribute_{i}", DataType.Text));
        }
        return schema;
    }
}
```

## Testing Best Practices

### 1. Test Organization

```
Editor/Tests/
├── Integration/     # Integration tests
├── Unit/           # Unit tests
├── GUI/            # GUI interaction tests
├── Visual/         # Visual regression tests
├── Performance/    # Performance tests
├── Utils/          # Test utilities
└── Mocks/          # Mock objects
```

### 2. Test Data Management

```csharp
// Editor/Tests/Utils/TestDataFactory.cs
public static class TestDataFactory
{
    public static DataScheme CreateTestScheme(string name = "TestScheme")
    {
        var scheme = new DataScheme(name);
        scheme.AddAttribute(new AttributeDefinition("StringField", DataType.Text));
        scheme.AddAttribute(new AttributeDefinition("IntField", DataType.Integer));
        scheme.AddAttribute(new AttributeDefinition("DateField", DataType.DateTime));
        return scheme;
    }
    
    public static List<DataEntry> CreateTestEntries(int count)
    {
        var entries = new List<DataEntry>();
        for (int i = 0; i < count; i++)
        {
            var entry = new DataEntry();
            entry.Add("StringField", $"Value_{i}");
            entry.Add("IntField", i);
            entry.Add("DateField", DateTime.Now.AddDays(i));
            entries.Add(entry);
        }
        return entries;
    }
}
```

### 3. Continuous Integration

Create a test runner script:

```csharp
// Editor/Scripts/TestRunner.cs
public static class TestRunner
{
    [MenuItem("Tests/Run All UI Tests")]
    public static void RunAllUITests()
    {
        var testRunner = EditorWindow.GetWindow<TestRunner>();
        testRunner.titleContent = new GUIContent("Test Runner");
        
        // Run tests programmatically
        var testAssembly = typeof(SchemaEditorWindowTests).Assembly;
        var testRunner = new NUnitTestRunner();
        testRunner.RunTests(testAssembly);
    }
}
```

## Implementation Recommendations

1. **Start with Unit Tests**: Begin with testing individual methods and properties
2. **Add Integration Tests**: Test component interactions and data flow
3. **Implement GUI Tests**: Test user interactions and UI behavior
4. **Add Visual Tests**: Prevent visual regressions
5. **Performance Tests**: Ensure UI remains responsive with large datasets

## Tools and Resources

- **Unity Test Runner**: Built-in testing framework
- **NUnit**: .NET testing framework
- **Unity UI Test Automation**: For automated UI testing
- **Visual Studio Test Tools**: For advanced testing scenarios
- **Custom Testing Utilities**: Project-specific test helpers

This comprehensive testing strategy ensures robust, maintainable Unity Editor UI components that provide a reliable user experience.