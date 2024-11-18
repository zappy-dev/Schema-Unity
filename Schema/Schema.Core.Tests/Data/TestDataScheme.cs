using System.Collections;
using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataScheme
{
    private DataScheme testScheme;
    private DataScheme emptyScheme;
    private const string EXISTING_STRING_ATTRIBUTE_NAME = "ExistingStringField";
    private const string EXISTING_INTEGER_ATTRIBUTE_NAME = "ExistingIntegerField";

    [SetUp]
    public void OnTestSetup()
    {
        Schema.Reset();

        emptyScheme = new DataScheme();
        
        testScheme = new DataScheme("Test");
        testScheme.AddAttribute(new AttributeDefinition(EXISTING_STRING_ATTRIBUTE_NAME, DataType.Text)).AssertPassed();
        testScheme.AddAttribute(new AttributeDefinition(EXISTING_INTEGER_ATTRIBUTE_NAME, DataType.Integer)).AssertPassed();
    }

    [Test, TestCaseSource(nameof(AddEntry_BadCases))]
    public void Test_AddEntry_BadCases(DataEntry badEntry)
    {
        testScheme.AddEntry(badEntry).AssertFailed();
    }

    [Test, TestCaseSource(nameof(AddEntry_BadCases))]
    public void Test_DeleteEntry_BadCases(DataEntry badEntry)
    {
        testScheme.DeleteEntry(badEntry).AssertFailed();
    }

    [Test]
    public void Test_DeleteEntry_GoodCases()
    {
        // Arrange
        DataEntry entry = new DataEntry();
        testScheme.AddEntry(entry);
        
        // Act
        testScheme.DeleteEntry(entry).AssertPassed();
    }

    private static IEnumerable AddEntry_BadCases
    {
        get
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new DataEntry
            {
                {"UnknownField", "data"}
            });
            yield return new TestCaseData(new DataEntry
            {
                {EXISTING_INTEGER_ATTRIBUTE_NAME, "baddata"}
            });
        }
    }

    [Test]
    public void Test_GetValuesForAttribute_Empty()
    {
        Assert.That(emptyScheme.GetValuesForAttribute("     ").Count(), Is.EqualTo(0));
    }

    [Test]
    public void Test_GetValuesForAttribute_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            emptyScheme.GetValuesForAttribute(attribute: null);
        });
    }

    [Test]
    public void Test_GetValuesForAttribute_UnknownAttribute()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            emptyScheme.GetValuesForAttribute(attribute: new AttributeDefinition());
        });
    }
    
    [Test]
    public void Test_ConvertAttributeType_Small()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        string attributeName = "Field1";
        dataScheme.AddAttribute(new AttributeDefinition(attributeName, DataType.Text));
        dataScheme.AddEntry(new DataEntry
        {
            { attributeName, "1" }
        });
        dataScheme.AddEntry(new DataEntry());
        
        // Act
        var conversionResponse = dataScheme.ConvertAttributeType(attributeName, DataType.Integer);
        
        // Assert
        Assert.IsTrue(conversionResponse.Passed);
        Assert.That(dataScheme.GetEntry(0).GetData(attributeName), Is.EqualTo(1));
        Assert.That(dataScheme.GetEntry(1).GetData(attributeName), Is.EqualTo(DataType.Integer.DefaultValue));
    }
    
    [Test]
    public void Test_ConvertAttributeType_BadConversion()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        string attributeName = "Field1";
        dataScheme.AddAttribute(new AttributeDefinition(attributeName, DataType.Integer));
        dataScheme.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { attributeName, 1 }
        }));
        
        // Act
        var res = dataScheme.ConvertAttributeType(attributeName, DataType.DateTime);
        
        // Assert
        res.AssertFailed();
        Assert.That(dataScheme.GetEntry(0).GetData(attributeName), Is.EqualTo(1));
    }

    [Test, TestCaseSource(nameof(UpdateAttributeNameTestCases))]
    public void Test_UpdateAttributeName(string previousName, DataType prevDataType, string newName, bool expected)
    {
        // Arrange
        testScheme.AddAttribute(new AttributeDefinition(previousName, prevDataType)).AssertPassed();
        testScheme.AddEntry(new DataEntry
        {
            { previousName, "FieldValue" }
        }).AssertPassed();
        testScheme.AddAttribute(new AttributeDefinition("OtherField", DataType.Text)).AssertPassed();
        testScheme.AddEntry(new DataEntry
        {
            { "OtherField", "FieldValue" }
        }).AssertPassed();
        
        // Act
        testScheme.UpdateAttributeName(previousName, newName).AssertCondition(expected);
    }
    
    [Test]
    public void Test_UpdateAttributeName_InvalidAttribute()
    {
        // Arrange
        testScheme.AddAttribute(new AttributeDefinition("OtherField", DataType.Text)).AssertPassed();
        testScheme.AddEntry(new DataEntry
        {
            { "OtherField", "FieldValue" }
        }).AssertPassed();
        
        // Act
        testScheme.UpdateAttributeName("InvalidField", "NewField").AssertFailed();
    }

    private static IEnumerable UpdateAttributeNameTestCases
    {
        get
        {
            yield return new TestCaseData("FieldA", DataType.Text, "", false);
            yield return new TestCaseData("FieldA", DataType.Text, "FieldA", false);
            yield return new TestCaseData("FieldA", DataType.Text, EXISTING_STRING_ATTRIBUTE_NAME, false);
            yield return new TestCaseData("FieldA", DataType.Text, "FieldB", true);
        }
    }

    [Test, TestCaseSource(nameof(UpdateAttributeName_ReferenceDataType_TestCases))]
    public void Test_UpdateAttributeName_ReferenceDataType(string refFieldName, DataType prevDataType, string newName, bool expected)
    {
        // Arrange
        var refSchemeName = "RefScheme";
        var refIdAttributeName = "RefId";
        var goodRefValue = "GoodRefValue";
        
        // create ref scheme with identifier field
        var refScheme = new DataScheme(refSchemeName);
        refScheme.AddAttribute(new AttributeDefinition(refIdAttributeName, DataType.Text, isIdentifier: true)).AssertPassed();
        refScheme.AddEntry(new DataEntry
        {
            { refIdAttributeName, goodRefValue }
        });
        
        // then load into memory
        refScheme.Load().AssertPassed();
        
        refScheme.GetIdentifierAttribute().TryAssert(out var identifierAttribute);

        identifierAttribute.CreateReferenceType().TryAssert(out var refType);

        // prepare referencing data scheme
        var dataScheme = new DataScheme("Foo");
        dataScheme.AddAttribute(new AttributeDefinition(refFieldName, refType)).AssertPassed();
        dataScheme.AddEntry(new DataEntry
        { 
            { refFieldName, goodRefValue }
        }).AssertPassed();
        
        // then load into memory
        dataScheme.Load().AssertPassed();

        // Act
        var newRefAttributeName = "NewField";
        refScheme.UpdateAttributeName(refIdAttributeName, newRefAttributeName).AssertCondition(expected);

        // Assert
        dataScheme.GetAttribute(refFieldName).TryAssert(out var refAttribute);
        var refDataType = refAttribute.DataType as ReferenceDataType;
        Assert.IsNotNull(refDataType);
        Assert.That(refDataType.ReferenceAttributeName, Is.EqualTo(newRefAttributeName));
    }

    private static IEnumerable UpdateAttributeName_ReferenceDataType_TestCases
    {
        get
        {
            yield return new TestCaseData("FieldA", DataType.Text, "GoodRefValue", true);
        }
    }

    [TestCase("Field", "Field2")]
    public void Test_CreateNewEntry(params string[] attributeNames)
    {
        string defaultValue = "Bar";
        foreach (var attributeName in attributeNames)
        {
            testScheme.AddAttribute(new AttributeDefinition(attributeName, DataType.Text, defaultValue: defaultValue));
        }
        
        var entry = testScheme.CreateNewEntry();
        foreach (var attributeName in attributeNames)
        {
            Assert.That(entry.GetDataAsString(attributeName), Is.EqualTo(defaultValue));
        }
    }

    [Test, TestCaseSource(nameof(BadAttributes))]
    public bool Test_AddAttribute_Bad(AttributeDefinition attribute)
    {
        // Act
        
        // Arrange
        var addResponse = testScheme.AddAttribute(attribute);
        
        return addResponse.Passed;
    }

    private static IEnumerable BadAttributes
    {
        get
        {
            yield return new TestCaseData(null).Returns(false);
            yield return new TestCaseData(new AttributeDefinition(null, DataType.Text)).Returns(false);
            yield return new TestCaseData(new AttributeDefinition("", DataType.Text)).Returns(false);
            yield return new TestCaseData(new AttributeDefinition(" ", DataType.Text)).Returns(false);
            yield return new TestCaseData(new AttributeDefinition("  ", DataType.Text)).Returns(false);
            yield return new TestCaseData(new AttributeDefinition(EXISTING_STRING_ATTRIBUTE_NAME, DataType.Text)).Returns(false);
        }
    }

    [Test]
    public void Test_SwapEntries()
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();

        var swapRes = testScheme.SwapEntries(0, 1);
        swapRes.AssertPassed();
        
        Assert.That(testScheme.GetEntry(0), Is.EqualTo(secondEntry));
        Assert.That(testScheme.GetEntry(1), Is.EqualTo(firstEntry));
    }

    [Test]
    public void Test_MoveUpEntry_BadMove()
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();
        
        var moveRes = testScheme.MoveUpEntry(firstEntry);
        moveRes.AssertFailed();
    }

    [Test]
    public void Test_MoveUpEntry_GoodMove()
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();
        
        var moveRes = testScheme.MoveUpEntry(secondEntry);
        moveRes.AssertPassed();
    }

    [Test]
    public void Test_MoveDownEntry_BadMove()
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();
        
        var moveRes = testScheme.MoveDownEntry(secondEntry);
        moveRes.AssertFailed();
    }

    [Test]
    public void Test_MoveDownEntry_GoodMove()
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();
        
        var moveRes = testScheme.MoveDownEntry(firstEntry);
        moveRes.AssertPassed();
    }

    [Test]
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    public void Test_MoveEntry(int moveIdx, bool expected)
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();

        var res = testScheme.MoveEntry(firstEntry, moveIdx);
        Assert.That(res.Passed, Is.EqualTo(expected));
    }
    
    [Test]
    public void Test_IncreaseAttributeRank()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        var firstAttribute = new AttributeDefinition("FirstAttribute", DataType.Text);
        var secondAttribute = new AttributeDefinition("SecondAttribute", DataType.Text);
        var thirdAttribute = new AttributeDefinition("ThirdAttribute", DataType.Text);
        dataScheme.AddAttribute(firstAttribute);
        dataScheme.AddAttribute(secondAttribute);
        dataScheme.AddAttribute(thirdAttribute);
        
        // Act
        var increaseResponse = dataScheme.IncreaseAttributeRank(secondAttribute);
        
        // Assert
        Assert.IsTrue(increaseResponse.Passed);
        Assert.That(dataScheme.GetAttribute(0), Is.EqualTo(secondAttribute));
        Assert.That(dataScheme.GetAttribute(1), Is.EqualTo(firstAttribute));
    }
    
    [Test]
    public void Test_DecreaseAttributeRank()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        var firstAttribute = new AttributeDefinition("FirstAttribute", DataType.Text);
        var secondAttribute = new AttributeDefinition("SecondAttribute", DataType.Text);
        var thirdAttribute = new AttributeDefinition("ThirdAttribute", DataType.Text);
        dataScheme.AddAttribute(firstAttribute);
        dataScheme.AddAttribute(secondAttribute);
        dataScheme.AddAttribute(thirdAttribute);
        
        // Act
        var increaseResponse = dataScheme.DecreaseAttributeRank(firstAttribute);
        
        // Assert
        Assert.IsTrue(increaseResponse.Passed);
        Assert.That(dataScheme.GetAttribute(0), Is.EqualTo(secondAttribute));
        Assert.That(dataScheme.GetAttribute(1), Is.EqualTo(firstAttribute));
    }

    [Test]
    public void Test_GetEntries_SortingCase()
    {
        var dataScheme = new DataScheme("Foo");
        var sortAttribute = "FirstAttribute";
        dataScheme.AddAttribute(new AttributeDefinition(sortAttribute, DataType.Text));
        dataScheme.AddAttribute(new AttributeDefinition("SecondAttribute", DataType.Text));

        dataScheme.AddEntry(new DataEntry
        {
            { sortAttribute, "a" },
        });
        dataScheme.AddEntry(new DataEntry
        {
            { sortAttribute, "c" },
        });
        dataScheme.AddEntry(new DataEntry
        {
            { sortAttribute, "b" },
        });
        
        Assert.That(dataScheme.GetEntries(), 
            Is.EqualTo(dataScheme.GetEntries(AttributeSortOrder.None)));
        Assert.That(dataScheme.GetEntries(), 
            Is.EqualTo(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.None))));
        Assert.That(dataScheme.GetEntries(), 
            Is.Not.EqualTo(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.Ascending))));
        Assert.That(dataScheme.GetEntries(), 
            Is.Not.EqualTo(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.Descending))));
        Assert.That(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.Descending)), 
            Is.EqualTo(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.Ascending)).Reverse()));
        Assert.That(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.Ascending)), 
            Is.EqualTo(dataScheme.GetEntries(new AttributeSortOrder(sortAttribute, SortOrder.Descending)).Reverse()));
    }

    [Test, TestCaseSource(nameof(BadAttributeSortOrders))]
    public void Test_GetEntries_BadCase(AttributeSortOrder sortOrder)
    {
        Assert.Throws<ArgumentException>(() => emptyScheme.GetEntries(sortOrder));
    }

    private static IEnumerable BadAttributeSortOrders
    {
        get
        {
            yield return new TestCaseData(new AttributeSortOrder("InvalidField", SortOrder.Ascending));
        }
    }

    [Test, TestCaseSource(nameof(DeleteAttributeTestCases))]
    public void Test_DeleteAttribute(AttributeDefinition attribute, bool expectedResult)
    {
        testScheme.DeleteAttribute(attribute).AssertCondition(expectedResult);
    }

    private static IEnumerable DeleteAttributeTestCases
    {
        get
        {
            yield return new TestCaseData(null, false);
            yield return new TestCaseData(new AttributeDefinition("UnknownField", DataType.Default), false);
        }
    }
}