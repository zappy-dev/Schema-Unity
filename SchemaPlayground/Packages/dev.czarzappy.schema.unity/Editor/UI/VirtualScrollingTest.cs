using UnityEditor;
using UnityEngine;
using Schema.Core;
using Schema.Core.Data;
using static Schema.Core.Schema;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// Test utility for generating large datasets to verify virtual scrolling performance
    /// </summary>
    public static class VirtualScrollingTest
    {
        
        
        private static void GenerateTestData(int entryCount)
        {
            // Create a test scheme with multiple data types
            var testScheme = new DataScheme($"VirtualScrollingTest_{entryCount}");
            
            // Add various attribute types
            var idAttribute = new AttributeDefinition(testScheme, "ID", DataType.Integer);
            testScheme.AddAttribute(idAttribute);
            testScheme.AddAttribute("Name", DataType.Text);
            testScheme.AddAttribute("Description", DataType.Text);
            testScheme.AddAttribute("IsActive", new BooleanDataType());
            testScheme.AddAttribute("CreatedDate", new DateTimeDataType());
            testScheme.AddAttribute("Value", DataType.Integer);
            
            // Generate test entries
            for (int i = 0; i < entryCount; i++)
            {
                var entry = testScheme.CreateNewEmptyEntry();
                testScheme.SetDataOnEntry(entry, "ID", i + 1);
                testScheme.SetDataOnEntry(entry,"Name", $"Test Entry {i + 1:D4}");
                testScheme.SetDataOnEntry(entry,"Description", $"This is a test description for entry {i + 1}. It contains some text to make it longer and more realistic.");
                testScheme.SetDataOnEntry(entry,"IsActive", i % 3 == 0); // Every third entry is active
                testScheme.SetDataOnEntry(entry,"CreatedDate", System.DateTime.Now.AddDays(-i));
                testScheme.SetDataOnEntry(entry,"Value", Random.Range(1, 1000));
            }
            // HACK - setting idAttribute to an identifier after creating entries
            idAttribute.IsIdentifier = true;
            
            // Save the scheme
            var targetFilePath = $"{testScheme.SchemeName}.json";
            LoadDataScheme(testScheme, overwriteExisting: true, registerManifestEntry: true, importFilePath: targetFilePath);
            
            var result = SaveDataScheme(testScheme, alsoSaveManifest: false);
            
            if (result.Passed)
            {
                Debug.Log($"Successfully generated test data with {entryCount} entries in scheme '{testScheme.SchemeName}'");
                Debug.Log($"Virtual scrolling should be active for this dataset (threshold: 100 entries)");
            }
            else
            {
                Debug.LogError($"Failed to generate test data: {result.Message}");
            }
        }
        
        [MenuItem("Tools/Schema/Clear Test Data")]
        public static void ClearTestData()
        {
            if (GetScheme("VirtualScrollingTest").Try(out var testScheme))
            {
                var result = UnloadScheme("VirtualScrollingTest");
                if (result.Passed)
                {
                    Debug.Log("Successfully cleared test data");
                }
                else
                {
                    Debug.LogError($"Failed to clear test data: {result.Message}");
                }
            }
            else
            {
                Debug.Log("No test data found to clear");
            }
        }
    }
} 