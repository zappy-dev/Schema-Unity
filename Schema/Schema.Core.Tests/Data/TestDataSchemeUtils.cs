using System.Text;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataSchemeUtils
{
    private static readonly SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestDataSchemeUtils)
    };

    [SetUp]
    public async Task OnTestSetup()
    {
        _ = await TestFixtureSetup.Initialize(Context);
    }

    [Test]
    public void Test_BuildAttributeDiffReport_IdenticalSchemes_ReturnsFalse()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        schemeA.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        schemeB.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.False);
        Assert.That(diffReport.ToString(), Is.Empty);
    }

    [Test]
    public void Test_BuildAttributeDiffReport_RemovedAttributes_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        schemeA.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();
        schemeA.AddAttribute(Context, "Field3", DataType.Boolean).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Removed attributes:"));
        Assert.That(report, Does.Contain("Field2 (Integer)"));
        Assert.That(report, Does.Contain("Field3 (Boolean)"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_AddedAttributes_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        schemeB.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();
        schemeB.AddAttribute(Context, "Field3", DataType.Boolean).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Added attributes:"));
        Assert.That(report, Does.Contain("Field2 (Integer)"));
        Assert.That(report, Does.Contain("Field3 (Boolean)"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedDataType_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Integer).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("DataType: String -> Integer"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedIsIdentifier_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text, isIdentifier: false).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Text, isIdentifier: true).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("IsIdentifier: False -> True"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedShouldPublish_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        var attrA = new AttributeDefinition(schemeA, "Field1", DataType.Text) { ShouldPublish = true };
        schemeA.AddAttribute(Context, attrA).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        var attrB = new AttributeDefinition(schemeB, "Field1", DataType.Text) { ShouldPublish = false };
        schemeB.AddAttribute(Context, attrB).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("ShouldPublish: True -> False"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedAttributeToolTip_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        var attrA = new AttributeDefinition(schemeA, "Field1", DataType.Text) { AttributeToolTip = "Old Tooltip" };
        schemeA.AddAttribute(Context, attrA).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        var attrB = new AttributeDefinition(schemeB, "Field1", DataType.Text) { AttributeToolTip = "New Tooltip" };
        schemeB.AddAttribute(Context, attrB).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("AttributeToolTip: 'Old Tooltip' -> 'New Tooltip'"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedColumnWidth_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        var attrA = new AttributeDefinition(schemeA, "Field1", DataType.Text) { ColumnWidth = 100 };
        schemeA.AddAttribute(Context, attrA).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        var attrB = new AttributeDefinition(schemeB, "Field1", DataType.Text) { ColumnWidth = 200 };
        schemeB.AddAttribute(Context, attrB).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("ColumnWidth: 100 -> 200"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedDefaultValue_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text, defaultValue: "OldDefault").AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Text, defaultValue: "NewDefault").AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("DefaultValue: 'OldDefault' -> 'NewDefault'"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedReferenceDataType_ReferenceSchemeName_ReturnsTrue()
    {
        // Arrange
        var refType1 = ReferenceDataTypeFactory.CreateReferenceDataType(Context, "RefScheme1", "ID", validateSchemeLoaded: false).AssertPassed();
        var refType2 = ReferenceDataTypeFactory.CreateReferenceDataType(Context, "RefScheme2", "ID", validateSchemeLoaded: false).AssertPassed();

        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, new AttributeDefinition(schemeA, "RefField", refType1)).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, new AttributeDefinition(schemeB, "RefField", refType2)).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: RefField"));
        Assert.That(report, Does.Contain("Reference Scheme: RefScheme1 -> RefScheme2"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_ModifiedReferenceDataType_ReferenceAttributeName_ReturnsTrue()
    {
        // Arrange
        var refType1 = ReferenceDataTypeFactory.CreateReferenceDataType(Context, "RefScheme", "ID1", validateSchemeLoaded: false).AssertPassed();
        var refType2 = ReferenceDataTypeFactory.CreateReferenceDataType(Context, "RefScheme", "ID2", validateSchemeLoaded: false).AssertPassed();

        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, new AttributeDefinition(schemeA, "RefField", refType1)).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, new AttributeDefinition(schemeB, "RefField", refType2)).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: RefField"));
        Assert.That(report, Does.Contain("Reference Attribute: ID1 -> ID2"));
    }

    // NOTE: This test case is not included because DataType equality only compares TypeName.
    // Two ReferenceDataTypes with the same ReferenceSchemeName and ReferenceAttributeName 
    // will be considered equal regardless of SupportsEmptyReferences, so BuildAttributeDiffReport
    // won't detect this difference. This would require changes to DataType.Equals to support.
    //
    // [Test]
    // public void Test_BuildAttributeDiffReport_ModifiedReferenceDataType_SupportsEmptyReferences_ReturnsTrue()
    // {
    //     var refType1 = ReferenceDataTypeFactory.CreateReferenceDataType(Context, "RefScheme", "ID") { SupportsEmptyReferences = false };
    //     var refType2 = ReferenceDataTypeFactory.CreateReferenceDataType(Context, "RefScheme", "ID") { SupportsEmptyReferences = true };
    //     // ... This will not be detected as a difference
    // }

    [Test]
    public void Test_BuildAttributeDiffReport_MultipleModifications_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        var attrA = new AttributeDefinition(schemeA, "Field1", DataType.Text)
        {
            AttributeToolTip = "Old Tooltip",
            ColumnWidth = 100,
            ShouldPublish = true
        };
        schemeA.AddAttribute(Context, attrA).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        var attrB = new AttributeDefinition(schemeB, "Field1", DataType.Integer)
        {
            AttributeToolTip = "New Tooltip",
            ColumnWidth = 200,
            ShouldPublish = false
        };
        schemeB.AddAttribute(Context, attrB).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("DataType: String -> Integer"));
        Assert.That(report, Does.Contain("ShouldPublish: True -> False"));
        Assert.That(report, Does.Contain("AttributeToolTip: 'Old Tooltip' -> 'New Tooltip'"));
        Assert.That(report, Does.Contain("ColumnWidth: 100 -> 200"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_CombinedAddedRemovedModified_ReturnsTrue()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        schemeA.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();
        schemeA.AddAttribute(Context, "Field3", DataType.Boolean).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Float).AssertPassed(); // Modified
        schemeB.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed(); // Unchanged
        schemeB.AddAttribute(Context, "Field4", DataType.Text).AssertPassed(); // Added
        // Field3 is removed

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        
        // Check removed
        Assert.That(report, Does.Contain("Removed attributes:"));
        Assert.That(report, Does.Contain("Field3"));
        
        // Check added
        Assert.That(report, Does.Contain("Added attributes:"));
        Assert.That(report, Does.Contain("Field4"));
        
        // Check modified
        Assert.That(report, Does.Contain("Modified attribute: Field1"));
        Assert.That(report, Does.Contain("DataType: String -> Float"));
        
        // Field2 should not be mentioned
        Assert.That(report, Does.Not.Contain("Modified attribute: Field2"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_EmptySchemes_ReturnsFalse()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        var schemeB = new DataScheme("TestScheme");

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.False);
        Assert.That(diffReport.ToString(), Is.Empty);
    }

    [Test]
    public void Test_BuildAttributeDiffReport_AttributesSortedAlphabetically()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Zebra", DataType.Text).AssertPassed();
        schemeA.AddAttribute(Context, "Apple", DataType.Integer).AssertPassed();
        schemeA.AddAttribute(Context, "Mango", DataType.Boolean).AssertPassed();

        var schemeB = new DataScheme("TestScheme");

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        var appleIndex = report.IndexOf("Apple");
        var mangoIndex = report.IndexOf("Mango");
        var zebraIndex = report.IndexOf("Zebra");
        
        Assert.That(appleIndex, Is.LessThan(mangoIndex), "Apple should appear before Mango");
        Assert.That(mangoIndex, Is.LessThan(zebraIndex), "Mango should appear before Zebra");
    }

    [Test]
    public void Test_BuildAttributeDiffReport_NullDefaultValues_DoesNotCrash()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        schemeA.AddAttribute(Context, "Field1", DataType.Text, defaultValue: null).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        schemeB.AddAttribute(Context, "Field1", DataType.Text, defaultValue: "NewDefault").AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("DefaultValue:"));
    }

    [Test]
    public void Test_BuildAttributeDiffReport_EmptyTooltipChanges_DetectedCorrectly()
    {
        // Arrange
        var schemeA = new DataScheme("TestScheme");
        var attrA = new AttributeDefinition(schemeA, "Field1", DataType.Text) { AttributeToolTip = "" };
        schemeA.AddAttribute(Context, attrA).AssertPassed();

        var schemeB = new DataScheme("TestScheme");
        var attrB = new AttributeDefinition(schemeB, "Field1", DataType.Text) { AttributeToolTip = "New Tooltip" };
        schemeB.AddAttribute(Context, attrB).AssertPassed();

        var diffReport = new StringBuilder();

        // Act
        var hasDiff = DataScheme.BuildAttributeDiffReport(Context, diffReport, schemeA, schemeB);

        // Assert
        Assert.That(hasDiff, Is.True);
        var report = diffReport.ToString();
        Assert.That(report, Does.Contain("AttributeToolTip: '' -> 'New Tooltip'"));
    }

    [Test]
    public void Test_TopologicalSort_Succeeds_OrderableSchemes()
    {
        // arrange
        var schemeA = new DataScheme("schemeA");
        var schemeAID = schemeA.AddAttribute(Context, "ID", DataType.Text, isIdentifier: true).AssertPassed();
        
        var schemeB = new DataScheme("schemeB");
        var schemeBID = schemeB.AddAttribute(Context, "ID", DataType.Text, isIdentifier: true).AssertPassed();
        var schemeC = new DataScheme("schemeC");
        var schemeD = new DataScheme("schemeD");
        
        // add references
        // self reference
        schemeA.AddAttribute(Context, "Field1", ReferenceDataTypeFactory.CreateReferenceDataType(Context, schemeA.SchemeName, schemeAID.AttributeName, validateSchemeLoaded: false).AssertPassed()).AssertPassed();
        
        // external reference
        schemeB.AddAttribute(Context, "Field1", ReferenceDataTypeFactory.CreateReferenceDataType(Context, schemeA.SchemeName, schemeAID.AttributeName, validateSchemeLoaded: false).AssertPassed()).AssertPassed();
        schemeC.AddAttribute(Context, "Field1", ReferenceDataTypeFactory.CreateReferenceDataType(Context, schemeA.SchemeName, schemeAID.AttributeName, validateSchemeLoaded: false).AssertPassed()).AssertPassed();
        schemeC.AddAttribute(Context, "Field2", ReferenceDataTypeFactory.CreateReferenceDataType(Context, schemeB.SchemeName, schemeBID.AttributeName, validateSchemeLoaded: false).AssertPassed()).AssertPassed();
        
        var list = new List<DataScheme> { schemeA, schemeB, schemeC, schemeD };

        var topologicalSort = DataScheme.TopologicalSortByReferences(Context, list).AssertPassed();
        
        Assert.That(topologicalSort, Is.Not.Null);
        Assert.That(topologicalSort.Count, Is.EqualTo(list.Count));

        foreach (var scheme in topologicalSort)
        {
            TestContext.WriteLine(scheme.ToString());
        }
    }
}

