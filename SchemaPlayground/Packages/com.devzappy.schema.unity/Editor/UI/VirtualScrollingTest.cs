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
        internal static SchemaResult GenerateTestData(int entryCount)
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
                if (testScheme.CreateNewEmptyEntry(ctx).Try(out var entry, out var newErr))
                {
                    return newErr.Cast();
                }
                
                testScheme.SetDataOnEntry(ctx, entry, "ID", i + 1);
                testScheme.SetDataOnEntry(ctx, entry, "Name", $"Test Entry {i + 1:D4}");
                testScheme.SetDataOnEntry(ctx, entry, "Description", $"This is a test description for entry {i + 1}. It contains some text to make it longer and more realistic.");
                testScheme.SetDataOnEntry(ctx, entry, "IsActive", i % 3 == 0); // Every third entry is active
                testScheme.SetDataOnEntry(ctx, entry, "CreatedDate", System.DateTime.Now.AddDays(-i));
                testScheme.SetDataOnEntry(ctx, entry, "Value", Random.Range(1, 1000));
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
            
            return result;
        }
    }
} 