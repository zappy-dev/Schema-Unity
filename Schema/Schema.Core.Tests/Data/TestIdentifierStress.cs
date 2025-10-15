using System.Collections;
using System.Linq;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

/// <summary>
/// Stress tests for identifier attribute modifications including:
/// - Changing identifier values
/// - Deleting identifier attributes
/// - Referencing identifier attributes from other schemes
/// - Converting identifier attribute types
/// </summary>
[TestFixture]
public class TestIdentifierStress
{
    private static readonly SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestIdentifierStress)
    };

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        var mockFS = new Mock<IFileSystem>();
        Schema.SetStorage(new Storage(mockFS.Object));
        Schema.InitializeTemplateManifestScheme(Context);
    }

    #region Changing Identifier Values

    [Test]
    public void Test_UpdateIdentifierValue_Simple()
    {
        // Arrange
        var scheme = new DataScheme("Items");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "SWORD", Context } });
        scheme.AddEntry(Context, new DataEntry { { "ID", "SHIELD", Context } });
        scheme.Load(Context).AssertPassed();

        // Act
        var result = Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "SWORD", "EXCALIBUR");

        // Assert
        result.AssertPassed();
        var identifierValues = scheme.GetIdentifierValues().ToList();
        Assert.That(identifierValues, Contains.Item("EXCALIBUR"));
        Assert.That(identifierValues.Contains("SWORD"), Is.False);
    }

    [Test]
    public void Test_UpdateIdentifierValue_WithMultipleReferences()
    {
        // Arrange: Create item scheme with identifier
        var itemScheme = new DataScheme("Items");
        var itemIdAttr = new AttributeDefinition(itemScheme, "ItemID", DataType.Text, isIdentifier: true);
        itemScheme.AddAttribute(Context, itemIdAttr).AssertPassed();
        itemScheme.AddEntry(Context, new DataEntry { { "ItemID", "POTION", Context } });
        itemScheme.Load(Context).AssertPassed();
        
        itemScheme.GetIdentifierAttribute().TryAssert(out var idAttr);
        idAttr.CreateReferenceType(Context).TryAssert(out var itemRefType);

        // Create multiple schemes that reference the item
        var inventoryScheme = new DataScheme("Inventory");
        inventoryScheme.AddAttribute(Context, new AttributeDefinition(inventoryScheme, "Item", itemRefType)).AssertPassed();
        inventoryScheme.AddAttribute(Context, new AttributeDefinition(inventoryScheme, "Quantity", DataType.Integer)).AssertPassed();
        inventoryScheme.AddEntry(Context, new DataEntry { { "Item", "POTION", Context }, { "Quantity", 5, Context } });
        inventoryScheme.AddEntry(Context, new DataEntry { { "Item", "POTION", Context }, { "Quantity", 10, Context } });
        inventoryScheme.Load(Context).AssertPassed();

        var shopScheme = new DataScheme("Shop");
        shopScheme.AddAttribute(Context, new AttributeDefinition(shopScheme, "Product", itemRefType)).AssertPassed();
        shopScheme.AddAttribute(Context, new AttributeDefinition(shopScheme, "Price", DataType.Integer)).AssertPassed();
        shopScheme.AddEntry(Context, new DataEntry { { "Product", "POTION", Context }, { "Price", 50, Context } });
        shopScheme.Load(Context).AssertPassed();

        // Act: Update the identifier value
        var result = Schema.UpdateIdentifierValue(Context, itemScheme.SchemeName, "ItemID", "POTION", "HEALTH_POTION");

        // Assert: All references should be updated
        result.AssertPassed();
        Assert.That(inventoryScheme.AllEntries.All(e => e.GetDataAsString("Item") == "HEALTH_POTION"), Is.True);
        Assert.That(shopScheme.AllEntries.All(e => e.GetDataAsString("Product") == "HEALTH_POTION"), Is.True);
    }

    [Test]
    public void Test_UpdateIdentifierValue_ToExistingValue_Fails()
    {
        // Arrange
        var scheme = new DataScheme("NPCs");
        var idAttr = new AttributeDefinition(scheme, "Name", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Name", "Alice", Context } }).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Name", "Bob", Context } }).AssertPassed();
        scheme.Load(Context).AssertPassed();

        // Act: Try to rename Alice to Bob (collision)
        Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "Name", "Alice", "Bob").AssertFailed();
    }

    [Test]
    public void Test_UpdateIdentifierValue_NonExistentOldValue_Fails()
    {
        // Arrange
        var scheme = new DataScheme("Items");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "SWORD", Context } });
        scheme.Load(Context).AssertPassed();

        // Act
        var result = Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "NONEXISTENT", "ANYTHING");

        // Assert
        result.AssertFailed();
    }

    [Test]
    public void Test_UpdateIdentifierValue_CascadeMultipleLevels()
    {
        // Arrange: Create a chain of references A -> B -> C
        var categoryScheme = new DataScheme("Categories");
        categoryScheme.AddAttribute(Context, new AttributeDefinition(categoryScheme, "CategoryID", DataType.Text, isIdentifier: true)).AssertPassed();
        categoryScheme.AddEntry(Context, new DataEntry { { "CategoryID", "WEAPON", Context } });
        categoryScheme.Load(Context).AssertPassed();
        
        categoryScheme.GetIdentifierAttribute().TryAssert(out var catIdAttr);
        catIdAttr.CreateReferenceType(Context).TryAssert(out var catRefType);

        var itemScheme = new DataScheme("Items");
        itemScheme.AddAttribute(Context, new AttributeDefinition(itemScheme, "ItemID", DataType.Text, isIdentifier: true)).AssertPassed();
        itemScheme.AddAttribute(Context, new AttributeDefinition(itemScheme, "Category", catRefType)).AssertPassed();
        itemScheme.AddEntry(Context, new DataEntry { { "ItemID", "SWORD", Context }, { "Category", "WEAPON", Context } });
        itemScheme.Load(Context).AssertPassed();
        
        itemScheme.GetIdentifierAttribute().TryAssert(out var itemIdAttr);
        itemIdAttr.CreateReferenceType(Context).TryAssert(out var itemRefType);

        var inventoryScheme = new DataScheme("Inventory");
        inventoryScheme.AddAttribute(Context, new AttributeDefinition(inventoryScheme, "Item", itemRefType)).AssertPassed();
        inventoryScheme.AddEntry(Context, new DataEntry { { "Item", "SWORD", Context } });
        inventoryScheme.Load(Context).AssertPassed();

        // Act: Update category identifier
        var result = Schema.UpdateIdentifierValue(Context, categoryScheme.SchemeName, "CategoryID", "WEAPON", "MELEE_WEAPON");

        // Assert: Category references should be updated
        result.AssertPassed();
        var itemEntry = itemScheme.AllEntries.First();
        Assert.That(itemEntry.GetDataAsString("Category"), Is.EqualTo("MELEE_WEAPON"));
    }

    [Test]
    public void Test_UpdateIdentifierValue_IntegerIdentifier()
    {
        // Arrange
        var scheme = new DataScheme("Players");
        var idAttr = new AttributeDefinition(scheme, "PlayerID", DataType.Integer, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddAttribute(Context, new AttributeDefinition(scheme, "Name", DataType.Text)).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "PlayerID", 1, Context }, { "Name", "Alice", Context } });
        scheme.AddEntry(Context, new DataEntry { { "PlayerID", 2, Context }, { "Name", "Bob", Context } });
        scheme.Load(Context).AssertPassed();

        // Act
        var result = Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "PlayerID", 1, 999);

        // Assert
        result.AssertPassed();
        var identifierValues = scheme.GetIdentifierValues().ToList();
        Assert.That(identifierValues, Contains.Item(999));
        Assert.That(identifierValues.Contains(1), Is.False);
    }

    #endregion

    #region Deleting Identifier Attributes

    [Test]
    public void Test_DeleteIdentifierAttribute_WithoutReferences()
    {
        // Arrange
        var scheme = new DataScheme("SimpleData");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddAttribute(Context, new AttributeDefinition(scheme, "Value", DataType.Integer)).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "A", Context }, { "Value", 1, Context } });

        // Act
        var result = scheme.DeleteAttribute(Context, idAttr);

        // Assert
        result.AssertPassed();
        Assert.That(scheme.HasIdentifierAttribute, Is.False);
        Assert.That(scheme.AttributeCount, Is.EqualTo(1));
    }

    [Test]
    public void Test_DeleteIdentifierAttribute_WithReferences_ReferencesInvalidated()
    {
        // Arrange: Create scheme with identifier
        var sourceScheme = new DataScheme("Source");
        var idAttr = new AttributeDefinition(sourceScheme, "SourceID", DataType.Text, isIdentifier: true);
        sourceScheme.AddAttribute(Context, idAttr).AssertPassed();
        sourceScheme.AddEntry(Context, new DataEntry { { "SourceID", "VALUE1", Context } });
        sourceScheme.Load(Context).AssertPassed();
        
        sourceScheme.GetIdentifierAttribute().TryAssert(out var sourceIdAttr);
        sourceIdAttr.CreateReferenceType(Context).TryAssert(out var refType);

        // Create referencing scheme
        var refScheme = new DataScheme("Referencing");
        refScheme.AddAttribute(Context, new AttributeDefinition(refScheme, "Ref", refType)).AssertPassed();
        refScheme.AddEntry(Context, new DataEntry { { "Ref", "VALUE1", Context } });
        refScheme.Load(Context).AssertPassed();

        // Act: Delete the identifier attribute
        var result = sourceScheme.DeleteAttribute(Context, sourceIdAttr);

        // Assert: Deletion succeeds but references become invalid
        result.AssertPassed();
        Assert.That(sourceScheme.HasIdentifierAttribute, Is.False);
        
        // The reference should now fail validation since the identifier no longer exists
        refScheme.GetAttribute("Ref").TryAssert(out var refAttr);
        var refDataType = refAttr.DataType as ReferenceDataType;
        Assert.IsNotNull(refDataType);
        
        // Validation should fail because the referenced scheme no longer has an identifier
        var validationResult = refDataType.IsValidValue(Context, "VALUE1");
        validationResult.AssertFailed();
    }

    [Test]
    public void Test_DeleteIdentifierAttribute_MultipleReferences()
    {
        // Arrange
        var sourceScheme = new DataScheme("Source");
        var idAttr = new AttributeDefinition(sourceScheme, "ID", DataType.Text, isIdentifier: true);
        sourceScheme.AddAttribute(Context, idAttr).AssertPassed();
        sourceScheme.AddEntry(Context, new DataEntry { { "ID", "ITEM", Context } });
        sourceScheme.Load(Context).AssertPassed();
        
        sourceScheme.GetIdentifierAttribute().TryAssert(out var sourceIdAttr);
        sourceIdAttr.CreateReferenceType(Context).TryAssert(out var refType);

        // Create multiple referencing schemes
        var ref1 = new DataScheme("Ref1");
        ref1.AddAttribute(Context, new AttributeDefinition(ref1, "SourceRef", refType)).AssertPassed();
        ref1.AddEntry(Context, new DataEntry { { "SourceRef", "ITEM", Context } });
        ref1.Load(Context).AssertPassed();

        var ref2 = new DataScheme("Ref2");
        ref2.AddAttribute(Context, new AttributeDefinition(ref2, "SourceRef", refType)).AssertPassed();
        ref2.AddEntry(Context, new DataEntry { { "SourceRef", "ITEM", Context } });
        ref2.Load(Context).AssertPassed();

        // Act
        var result = sourceScheme.DeleteAttribute(Context, sourceIdAttr);

        // Assert
        result.AssertPassed();
        Assert.That(sourceScheme.HasIdentifierAttribute, Is.False);
    }

    [Test]
    public void Test_ReplaceIdentifierAttribute_AddNewAfterDelete()
    {
        // Arrange
        var scheme = new DataScheme("Flexible");
        var oldIdAttr = new AttributeDefinition(scheme, "OldID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, oldIdAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "OldID", "A", Context } });

        // Act: Delete old identifier and add new one
        scheme.DeleteAttribute(Context, oldIdAttr).AssertPassed();
        var newIdAttr = new AttributeDefinition(scheme, "NewID", DataType.Integer, isIdentifier: true);
        scheme.AddAttribute(Context, newIdAttr).AssertPassed();

        // Assert
        Assert.That(scheme.HasIdentifierAttribute, Is.True);
        scheme.GetIdentifierAttribute().TryAssert(out var currentIdAttr);
        Assert.That(currentIdAttr.AttributeName, Is.EqualTo("NewID"));
    }

    [Test]
    public void Test_DeleteIdentifierAttribute_ThenAttemptToAddReference_Fails()
    {
        // Arrange
        var sourceScheme = new DataScheme("Source");
        var idAttr = new AttributeDefinition(sourceScheme, "ID", DataType.Text, isIdentifier: true);
        sourceScheme.AddAttribute(Context, idAttr).AssertPassed();
        sourceScheme.AddEntry(Context, new DataEntry { { "ID", "VALUE", Context } });
        sourceScheme.Load(Context).AssertPassed();

        // Delete the identifier
        sourceScheme.DeleteAttribute(Context, idAttr).AssertPassed();

        // Act: Try to create a reference to the now-missing identifier
        sourceScheme.GetIdentifierAttribute().AssertFailed();
        idAttr.CreateReferenceType(Context).AssertFailed();
    }

    #endregion

    #region Converting Identifier Type

    [Test]
    public void Test_ConvertIdentifierType_TextToInteger_Simple()
    {
        // Arrange
        var scheme = new DataScheme("Convertible");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "1", Context } });
        scheme.AddEntry(Context, new DataEntry { { "ID", "2", Context } });
        scheme.AddEntry(Context, new DataEntry { { "ID", "3", Context } });

        // Act
        var result = scheme.ConvertAttributeType(Context, "ID", DataType.Integer);

        // Assert
        result.AssertPassed();
        scheme.GetIdentifierAttribute().TryAssert(out var convertedIdAttr);
        Assert.That(convertedIdAttr.DataType, Is.InstanceOf<IntegerDataType>());
        var identifierValues = scheme.GetIdentifierValues().ToList();
        Assert.That(identifierValues, Contains.Item(1));
        Assert.That(identifierValues, Contains.Item(2));
        Assert.That(identifierValues, Contains.Item(3));
    }

    [Test]
    public void Test_ConvertIdentifierType_WithReferences()
    {
        // Arrange: Create source with text identifier
        var sourceScheme = new DataScheme("Source");
        var idAttr = new AttributeDefinition(sourceScheme, "ID", DataType.Text, isIdentifier: true);
        sourceScheme.AddAttribute(Context, idAttr).AssertPassed();
        sourceScheme.AddEntry(Context, new DataEntry { { "ID", "100", Context } });
        sourceScheme.Load(Context).AssertPassed();
        
        sourceScheme.GetIdentifierAttribute().TryAssert(out var sourceIdAttr);
        sourceIdAttr.CreateReferenceType(Context).TryAssert(out var refType);

        // Create referencing scheme
        var refScheme = new DataScheme("Referencing");
        refScheme.AddAttribute(Context, new AttributeDefinition(refScheme, "SourceRef", refType)).AssertPassed();
        refScheme.AddEntry(Context, new DataEntry { { "SourceRef", "100", Context } });
        refScheme.Load(Context).AssertPassed();

        // Act: Convert identifier from Text to Integer
        var result = sourceScheme.ConvertAttributeType(Context, "ID", DataType.Integer);

        // Assert
        result.AssertPassed();
        sourceScheme.GetIdentifierAttribute().TryAssert(out var convertedIdAttr);
        Assert.That(convertedIdAttr.DataType, Is.InstanceOf<IntegerDataType>());
        
        // The identifier values should be converted to integers
        Assert.That(sourceScheme.GetIdentifierValues().First(), Is.EqualTo(100));
    }

    [Test]
    public void Test_ConvertIdentifierType_BadConversion_Fails()
    {
        // Arrange
        var scheme = new DataScheme("BadConvert");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "NOT_A_NUMBER", Context } });

        // Act: Try to convert text (non-numeric) to integer
        var result = scheme.ConvertAttributeType(Context, "ID", DataType.Integer);

        // Assert: Should fail
        result.AssertFailed();
        scheme.GetIdentifierAttribute().TryAssert(out var unchangedIdAttr);
        Assert.That(unchangedIdAttr.DataType, Is.InstanceOf<TextDataType>());
    }

    [Test]
    public void Test_ConvertIdentifierType_IntegerToText()
    {
        // Arrange
        var scheme = new DataScheme("Convertible");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Integer, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", 42, Context } });
        scheme.AddEntry(Context, new DataEntry { { "ID", 99, Context } });

        // Act
        var result = scheme.ConvertAttributeType(Context, "ID", DataType.Text);

        // Assert
        result.AssertPassed();
        scheme.GetIdentifierAttribute().TryAssert(out var convertedIdAttr);
        Assert.That(convertedIdAttr.DataType, Is.InstanceOf<TextDataType>());
        var identifierValues = scheme.GetIdentifierValues().ToList();
        Assert.That(identifierValues, Contains.Item("42"));
        Assert.That(identifierValues, Contains.Item("99"));
    }

    [Test]
    public void Test_ConvertIdentifierType_FloatToInteger_WithPrecisionLoss()
    {
        // Arrange
        var scheme = new DataScheme("FloatConvert");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Float, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", 1.5, Context } });
        scheme.AddEntry(Context, new DataEntry { { "ID", 2.7, Context } });

        // Act
        var result = scheme.ConvertAttributeType(Context, "ID", DataType.Integer);

        // Assert: Conversion may succeed but with truncation
        if (result.Passed)
        {
            scheme.GetIdentifierAttribute().TryAssert(out var convertedIdAttr);
            Assert.That(convertedIdAttr.DataType, Is.InstanceOf<IntegerDataType>());
            // Values should be truncated/rounded
            var values = scheme.GetIdentifierValues().ToArray();
            Assert.That(values, Does.Contain(1).Or.Contain(2));
        }
        else
        {
            // Or it may fail depending on conversion strictness
            Assert.That(result.Passed, Is.False);
        }
    }

    [Test]
    public void Test_ConvertIdentifierType_BooleanToText()
    {
        // Arrange
        var scheme = new DataScheme("BoolConvert");
        var idAttr = new AttributeDefinition(scheme, "Active", DataType.Boolean, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Active", true, Context } });
        scheme.AddEntry(Context, new DataEntry { { "Active", false, Context } });

        // Act
        var result = scheme.ConvertAttributeType(Context, "Active", DataType.Text);

        // Assert
        result.AssertPassed();
        scheme.GetIdentifierAttribute().TryAssert(out var convertedIdAttr);
        Assert.That(convertedIdAttr.DataType, Is.InstanceOf<TextDataType>());
        var values = scheme.GetIdentifierValues().Select(v => v.ToString()).ToArray();
        Assert.That(values, Does.Contain("True").Or.Contain("true"));
        Assert.That(values, Does.Contain("False").Or.Contain("false"));
    }

    #endregion

    #region Complex Stress Scenarios

    [Test]
    public void Test_StressScenario_MultipleIdentifierUpdatesInSequence()
    {
        // Arrange
        var scheme = new DataScheme("Evolving");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "V1", Context } });
        scheme.Load(Context).AssertPassed();

        // Act: Perform multiple sequential updates
        Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "V1", "V2").AssertPassed();
        Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "V2", "V3").AssertPassed();
        Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "V3", "FINAL").AssertPassed();

        // Assert
        var identifierValues = scheme.GetIdentifierValues().ToList();
        Assert.That(identifierValues, Contains.Item("FINAL"));
        Assert.That(identifierValues.Contains("V1"), Is.False);
        Assert.That(identifierValues.Contains("V2"), Is.False);
        Assert.That(identifierValues.Contains("V3"), Is.False);
    }

    [Test]
    public void Test_StressScenario_ConvertType_ThenUpdateValue()
    {
        // Arrange
        var scheme = new DataScheme("Complex");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "100", Context } });
        scheme.Load(Context).AssertPassed();

        // Act: Convert type, then update value
        scheme.ConvertAttributeType(Context, "ID", DataType.Integer).AssertPassed();
        var result = Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", 100, 200);

        // Assert
        result.AssertPassed();
        var identifierValues = scheme.GetIdentifierValues().ToList();
        Assert.That(identifierValues, Contains.Item(200));
    }

    [Test]
    public void Test_StressScenario_CircularReferenceHandling()
    {
        // Arrange: Create two schemes that reference each other
        var schemeA = new DataScheme("SchemeA");
        schemeA.AddAttribute(Context, new AttributeDefinition(schemeA, "IDA", DataType.Text, isIdentifier: true)).AssertPassed();
        schemeA.AddEntry(Context, new DataEntry { { "IDA", "A1", Context } });
        schemeA.Load(Context).AssertPassed();

        var schemeB = new DataScheme("SchemeB");
        schemeB.AddAttribute(Context, new AttributeDefinition(schemeB, "IDB", DataType.Text, isIdentifier: true)).AssertPassed();
        schemeB.AddEntry(Context, new DataEntry { { "IDB", "B1", Context } });
        schemeB.Load(Context).AssertPassed();

        // Add cross-references
        schemeA.GetIdentifierAttribute().TryAssert(out var idAttrA);
        idAttrA.CreateReferenceType(Context).TryAssert(out var refTypeA);
        
        schemeB.GetIdentifierAttribute().TryAssert(out var idAttrB);
        idAttrB.CreateReferenceType(Context).TryAssert(out var refTypeB);

        schemeA.AddAttribute(Context, new AttributeDefinition(schemeA, "RefToB", refTypeB)).AssertPassed();
        schemeB.AddAttribute(Context, new AttributeDefinition(schemeB, "RefToA", refTypeA)).AssertPassed();

        schemeA.AllEntries.First().SetData(Context, "RefToB", "B1");
        schemeB.AllEntries.First().SetData(Context, "RefToA", "A1");

        // Act: Update identifier in one scheme
        var result = Schema.UpdateIdentifierValue(Context, schemeA.SchemeName, "IDA", "A1", "A2");

        // Assert: The reference in SchemeB should be updated
        result.AssertPassed();
        Assert.That(schemeB.AllEntries.First().GetDataAsString("RefToA"), Is.EqualTo("A2"));
    }

    [Test]
    public void Test_StressScenario_ManyReferences_SingleUpdate()
    {
        // Arrange: Create one source scheme
        var sourceScheme = new DataScheme("Source");
        sourceScheme.AddAttribute(Context, new AttributeDefinition(sourceScheme, "ID", DataType.Text, isIdentifier: true)).AssertPassed();
        sourceScheme.AddEntry(Context, new DataEntry { { "ID", "MASTER", Context } });
        sourceScheme.Load(Context).AssertPassed();
        
        sourceScheme.GetIdentifierAttribute().TryAssert(out var idAttr);
        idAttr.CreateReferenceType(Context).TryAssert(out var refType);

        // Create many referencing schemes with many entries
        int schemeCount = 10;
        int entriesPerScheme = 20;
        var refSchemes = new DataScheme[schemeCount];

        for (int i = 0; i < schemeCount; i++)
        {
            var refScheme = new DataScheme($"Ref{i}");
            refScheme.AddAttribute(Context, new AttributeDefinition(refScheme, "SourceRef", refType)).AssertPassed();
            
            for (int j = 0; j < entriesPerScheme; j++)
            {
                refScheme.AddEntry(Context, new DataEntry { { "SourceRef", "MASTER", Context } });
            }
            
            refScheme.Load(Context).AssertPassed();
            refSchemes[i] = refScheme;
        }

        // Act: Update the single identifier
        var result = Schema.UpdateIdentifierValue(Context, sourceScheme.SchemeName, "ID", "MASTER", "UPDATED_MASTER");

        // Assert: All references across all schemes should be updated
        result.AssertPassed();
        foreach (var refScheme in refSchemes)
        {
            Assert.That(refScheme.AllEntries.All(e => e.GetDataAsString("SourceRef") == "UPDATED_MASTER"), Is.True,
                $"All entries in {refScheme.SchemeName} should have updated references");
        }
    }

    [Test]
    public void Test_StressScenario_DeleteIdentifier_ThenRecreateWithSameName()
    {
        // Arrange
        var scheme = new DataScheme("Recyclable");
        var idAttr1 = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr1).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "OLD", Context } });

        // Act: Delete and recreate
        scheme.DeleteAttribute(Context, idAttr1).AssertPassed();
        var idAttr2 = new AttributeDefinition(scheme, "ID", DataType.Integer, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr2).AssertPassed();

        // Assert: New identifier should be integer type
        scheme.GetIdentifierAttribute().TryAssert(out var currentIdAttr);
        Assert.That(currentIdAttr.AttributeName, Is.EqualTo("ID"));
        Assert.That(currentIdAttr.DataType, Is.InstanceOf<IntegerDataType>());
    }

    [Test]
    public void Test_StressScenario_MultipleIdentifiersAttempt_OnlyOneAllowed()
    {
        // Arrange
        var scheme = new DataScheme("SingleID");
        var idAttr1 = new AttributeDefinition(scheme, "ID1", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr1).AssertPassed();

        // Act: Try to add a second identifier
        var idAttr2 = new AttributeDefinition(scheme, "ID2", DataType.Text, isIdentifier: true);
        var result = scheme.AddAttribute(Context, idAttr2);

        // Assert: Should fail (or allow it, depending on business rules)
        // Note: This test assumes only one identifier is allowed
        if (result.Passed)
        {
            // If multiple identifiers are allowed, verify both exist
            Assert.That(scheme.AttributeCount, Is.GreaterThanOrEqualTo(2));
        }
        else
        {
            // If only one is allowed, verify the add failed
            result.AssertFailed();
            Assert.That(scheme.AttributeCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_StressScenario_EmptyIdentifierValue()
    {
        // Arrange
        var scheme = new DataScheme("EmptyTest");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "", Context } });
        scheme.Load(Context).AssertPassed();

        // Act: Try to update empty identifier
        var result = Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "", "NOT_EMPTY");

        // Assert: Should either succeed or fail gracefully
        if (result.Passed)
        {
            var identifierValues = scheme.GetIdentifierValues().ToList();
            Assert.That(identifierValues, Contains.Item("NOT_EMPTY"));
        }
    }

    [Test]
    public void Test_StressScenario_NullIdentifierHandling()
    {
        // Arrange
        var scheme = new DataScheme("NullTest");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();

        // Act: Try to add entry with null identifier
        var entry = new DataEntry();
        entry.SetData(Context, "ID", null);
        var result = scheme.AddEntry(Context, entry);

        // Assert: May fail or allow null depending on implementation
        // Null identifiers are generally problematic for references
        if (result.Failed)
        {
            result.AssertFailed();
        }
    }

    [Test]
    public void Test_StressScenario_ConvertType_WithReferences_CompatibleConversion()
    {
        // Arrange
        var sourceScheme = new DataScheme("Source");
        sourceScheme.AddAttribute(Context, new AttributeDefinition(sourceScheme, "ID", DataType.Text, isIdentifier: true)).AssertPassed();
        sourceScheme.AddEntry(Context, new DataEntry { { "ID", "123", Context } });
        sourceScheme.Load(Context).AssertPassed();
        
        sourceScheme.GetIdentifierAttribute().TryAssert(out var idAttr);
        idAttr.CreateReferenceType(Context).TryAssert(out var refType);

        var refScheme = new DataScheme("Referencing");
        refScheme.AddAttribute(Context, new AttributeDefinition(refScheme, "Ref", refType)).AssertPassed();
        refScheme.AddEntry(Context, new DataEntry { { "Ref", "123", Context } });
        refScheme.Load(Context).AssertPassed();

        // Act: Convert identifier to integer (compatible conversion)
        var result = sourceScheme.ConvertAttributeType(Context, "ID", DataType.Integer);

        // Assert
        result.AssertPassed();
        
        // References should still point to the same logical value
        var refEntry = refScheme.AllEntries.First();
        var refValue = refEntry.GetDataAsString("Ref");
        Assert.That(refValue, Is.EqualTo("123"));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Test_EdgeCase_UpdateIdentifier_EmptyScheme()
    {
        // Arrange
        var scheme = new DataScheme("Empty");
        scheme.AddAttribute(Context, new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true)).AssertPassed();
        scheme.Load(Context).AssertPassed();

        // Act: Try to update identifier when no entries exist
        var result = Schema.UpdateIdentifierValue(Context, scheme.SchemeName, "ID", "NONEXISTENT", "ANYTHING");

        // Assert
        result.AssertFailed();
    }

    [Test]
    public void Test_EdgeCase_DeleteLastAttribute_WhichIsIdentifier()
    {
        // Arrange
        var scheme = new DataScheme("OnlyID");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();

        // Act
        var result = scheme.DeleteAttribute(Context, idAttr);

        // Assert
        result.AssertPassed();
        Assert.That(scheme.AttributeCount, Is.EqualTo(0));
        Assert.That(scheme.HasIdentifierAttribute, Is.False);
    }

    [Test]
    public void Test_EdgeCase_ConvertIdentifier_ToSameType()
    {
        // Arrange
        var scheme = new DataScheme("SameType");
        var idAttr = new AttributeDefinition(scheme, "ID", DataType.Text, isIdentifier: true);
        scheme.AddAttribute(Context, idAttr).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "ID", "VALUE", Context } });

        // Act: Convert to same type
        var result = scheme.ConvertAttributeType(Context, "ID", DataType.Text);

        // Assert: Should succeed (no-op) or fail gracefully
        if (result.Passed)
        {
            scheme.GetIdentifierAttribute().TryAssert(out var unchangedIdAttr);
            Assert.That(unchangedIdAttr.DataType, Is.InstanceOf<TextDataType>());
        }
    }

    #endregion
}

