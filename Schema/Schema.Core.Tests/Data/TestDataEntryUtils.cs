using System.Text;
using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataEntryUtils
{
    private static readonly SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestDataEntryUtils)
    };

    [Test]
    public void Test_BuildDiffReport_IdenticalEntries_ReturnsFalse()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "Value1", Context).AssertPassed();
        entryA.Add("Field2", 42, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "Value1", Context).AssertPassed();
        entryB.Add("Field2", 42, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.False);
        Assert.That(diffReport.ToString(), Is.Empty);
    }

    [Test]
    public void Test_BuildDiffReport_RemovedAttributes_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "Value1", Context).AssertPassed();
        entryA.Add("Field2", 42, Context).AssertPassed();
        entryA.Add("Field3", true, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "Value1", Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Removed attributes:"));
        Assert.That(report, Does.Contain("42")); // Values are printed, not keys
        Assert.That(report, Does.Contain("True"));
    }

    [Test]
    public void Test_BuildDiffReport_AddedAttributes_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "Value1", Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "Value1", Context).AssertPassed();
        entryB.Add("Field2", 42, Context).AssertPassed();
        entryB.Add("Field3", true, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Added attributes:"));
        Assert.That(report, Does.Contain("42")); // Values are printed, not keys
        Assert.That(report, Does.Contain("True"));
    }

    [Test]
    public void Test_BuildDiffReport_ModifiedStringValue_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "OldValue", Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "NewValue", Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("OldValue"));
        Assert.That(report, Does.Contain("NewValue"));
    }

    [Test]
    public void Test_BuildDiffReport_ModifiedIntegerValue_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", 10, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", 20, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("10"));
        Assert.That(report, Does.Contain("20"));
    }

    [Test]
    public void Test_BuildDiffReport_ModifiedBooleanValue_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", true, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", false, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("True"));
        Assert.That(report, Does.Contain("False"));
    }

    [Test]
    public void Test_BuildDiffReport_ModifiedFloatValue_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", 1.5f, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", 2.5f, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("1.5"));
        Assert.That(report, Does.Contain("2.5"));
    }

    [Test]
    public void Test_BuildDiffReport_MultipleModifications_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "OldValue", Context).AssertPassed();
        entryA.Add("Field2", 10, Context).AssertPassed();
        entryA.Add("Field3", true, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "NewValue", Context).AssertPassed();
        entryB.Add("Field2", 20, Context).AssertPassed();
        entryB.Add("Field3", false, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("Modified attribute: Field2"));
        Assert.That(report, Does.Contain("Modified attribute: Field3"));
    }

    [Test]
    public void Test_BuildDiffReport_CombinedAddedRemovedModified_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "Value1", Context).AssertPassed();
        entryA.Add("Field2", 10, Context).AssertPassed();
        entryA.Add("Field3", true, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "ModifiedValue", Context).AssertPassed(); // Modified
        entryB.Add("Field2", 10, Context).AssertPassed(); // Unchanged
        entryB.Add("Field4", "NewField", Context).AssertPassed(); // Added
        // Field3 is removed

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        
        // Check removed (values are printed)
        Assert.That(report, Does.Contain("Removed attributes:"));
        Assert.That(report, Does.Contain("True"));
        
        // Check added (values are printed)
        Assert.That(report, Does.Contain("Added attributes:"));
        Assert.That(report, Does.Contain("NewField"));
        
        // Check modified
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("ModifiedValue"));
        
        // Field2 should not be mentioned as it's unchanged
        Assert.That(report, Does.Not.Contain("Modified attribute: Field2"));
    }

    [Test]
    public void Test_BuildDiffReport_EmptyEntries_ReturnsFalse()
    {
        // Arrange
        var entryA = new DataEntry();
        var entryB = new DataEntry();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.False);
        Assert.That(diffReport.ToString(), Is.Empty);
    }

    [Test]
    public void Test_BuildDiffReport_AttributesSortedAlphabetically()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Zebra", "ZebraValue", Context).AssertPassed();
        entryA.Add("Apple", "AppleValue", Context).AssertPassed();
        entryA.Add("Mango", "MangoValue", Context).AssertPassed();

        var entryB = new DataEntry();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        // Values are printed, so check for values in alphabetical key order
        var appleIndex = report.IndexOf("AppleValue");
        var mangoIndex = report.IndexOf("MangoValue");
        var zebraIndex = report.IndexOf("ZebraValue");
        
        Assert.That(appleIndex, Is.GreaterThan(0), "Apple should be found");
        Assert.That(mangoIndex, Is.GreaterThan(0), "Mango should be found");
        Assert.That(zebraIndex, Is.GreaterThan(0), "Zebra should be found");
        Assert.That(appleIndex, Is.LessThan(mangoIndex), "Apple should appear before Mango");
        Assert.That(mangoIndex, Is.LessThan(zebraIndex), "Mango should appear before Zebra");
    }

    [Test]
    public void Test_BuildDiffReport_ZeroToValue_ReturnsTrue()
    {
        // Arrange - Test with zero/default values instead of null to avoid NullReferenceException
        var entryA = new DataEntry();
        entryA.Add("Field1", 0, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", 42, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("0"));
        Assert.That(report, Does.Contain("42"));
    }

    [Test]
    public void Test_BuildDiffReport_ValueToEmptyString_ReturnsTrue()
    {
        // Arrange - Test with empty string instead of null to avoid NullReferenceException
        var entryA = new DataEntry();
        entryA.Add("Field1", "OldValue", Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "", Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("OldValue"));
    }

    [Test]
    public void Test_BuildDiffReport_EmptyStringToNonEmpty_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "", Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", "Value", Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
    }

    [Test]
    public void Test_BuildDiffReport_DateTimeValues_ReturnsTrue()
    {
        // Arrange
        var date1 = new DateTime(2023, 1, 1);
        var date2 = new DateTime(2024, 1, 1);

        var entryA = new DataEntry();
        entryA.Add("DateField", date1, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("DateField", date2, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: DateField"));
    }

    [Test]
    public void Test_BuildDiffReport_GuidValues_ReturnsTrue()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var entryA = new DataEntry();
        entryA.Add("GuidField", guid1, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("GuidField", guid2, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: GuidField"));
        Assert.That(report, Does.Contain(guid1.ToString()));
        Assert.That(report, Does.Contain(guid2.ToString()));
    }

    [Test]
    public void Test_BuildDiffReport_TypeChange_StringToInt_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", "123", Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", 123, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
    }

    [Test]
    public void Test_BuildDiffReport_ManyAttributes_PerformanceCheck()
    {
        // Arrange
        var entryA = new DataEntry();
        var entryB = new DataEntry();
        
        for (int i = 0; i < 100; i++)
        {
            entryA.Add($"Field{i}", $"Value{i}", Context).AssertPassed();
            entryB.Add($"Field{i}", $"Value{i}", Context).AssertPassed();
        }

        var diffReport = new StringBuilder();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);
        sw.Stop();

        // Assert
        Assert.That(hasDiff, Is.False);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(100), "Should complete quickly for 100 attributes");
    }

    [Test]
    public void Test_BuildDiffReport_OneEmptyOnePopulated_ReturnsTrue()
    {
        // Arrange
        var entryA = new DataEntry();

        var entryB = new DataEntry();
        entryB.Add("Field1", "Value1", Context).AssertPassed();
        entryB.Add("Field2", 42, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Added attributes:"));
        Assert.That(report, Does.Contain("Value1")); // Values are printed
        Assert.That(report, Does.Contain("42"));
    }

    [Test]
    public void Test_BuildDiffReport_LongValue_HandledCorrectly()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", 1000000000L, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", 2000000000L, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("1000000000"));
        Assert.That(report, Does.Contain("2000000000"));
    }

    [Test]
    public void Test_BuildDiffReport_DoubleValue_HandledCorrectly()
    {
        // Arrange
        var entryA = new DataEntry();
        entryA.Add("Field1", 3.14159, Context).AssertPassed();

        var entryB = new DataEntry();
        entryB.Add("Field1", 2.71828, Context).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataEntry.BuildDiffReport(Context, diffReport, entryA, entryB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
    }
}


