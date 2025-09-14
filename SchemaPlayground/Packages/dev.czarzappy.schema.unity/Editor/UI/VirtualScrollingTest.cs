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
    internal static class VirtualScrollingTest
    {
        internal static void GenerateTestData(int entryCount)
        {
            // Create a test scheme with multiple data types
            var testScheme = new DataScheme($"VirtualScrollingTest_{entryCount}");
            var ctx = new SchemaContext
            {
                Scheme = testScheme,
                Driver = "Generate_Test_Scheme",
            };
            
            // Add various attribute types
            var idAttribute = new AttributeDefinition(testScheme, "ID", DataType.Integer);
            testScheme.AddAttribute(ctx, idAttribute);
            testScheme.AddAttribute(ctx, "Name", DataType.Text);
            testScheme.AddAttribute(ctx, "Description", DataType.Text);
            testScheme.AddAttribute(ctx, "IsActive", new BooleanDataType());
            testScheme.AddAttribute(ctx, "CreatedDate", new DateTimeDataType());
            testScheme.AddAttribute(ctx, "Value", DataType.Integer);
            
            // Generate test entries
            for (int i = 0; i < entryCount; i++)
            {
                var entry = testScheme.CreateNewEmptyEntry(ctx);
                testScheme.SetDataOnEntry(entry, "ID", i + 1, context: ctx);
                testScheme.SetDataOnEntry(entry, "Name", $"Test Entry {i + 1:D4}", context: ctx);
                testScheme.SetDataOnEntry(entry, "Description", $"This is a test description for entry {i + 1}. It contains some text to make it longer and more realistic.", context: ctx);
                testScheme.SetDataOnEntry(entry, "IsActive", i % 3 == 0, context: ctx); // Every third entry is active
                testScheme.SetDataOnEntry(entry, "CreatedDate", System.DateTime.Now.AddDays(-i), context: ctx);
                testScheme.SetDataOnEntry(entry, "Value", Random.Range(1, 1000), context: ctx);
            }
            // HACK - setting idAttribute to an identifier after creating entries
            idAttribute.IsIdentifier = true;
            
            // Save the scheme
            var targetFilePath = $"{testScheme.SchemeName}.json";
            LoadDataScheme(ctx, testScheme, overwriteExisting: true, registerManifestEntry: true, importFilePath: targetFilePath);
            
            var result = SaveDataScheme(ctx, testScheme, alsoSaveManifest: false);
            
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
    }
} 