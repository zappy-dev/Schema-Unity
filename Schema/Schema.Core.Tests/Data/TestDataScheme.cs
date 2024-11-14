using System.Collections;
using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataScheme
{
    private DataScheme testScheme;
    private const string EXISTING_ATTRIBUTE_NAME = "ExistingField";

    [SetUp]
    public void OnTestSetup()
    {
        testScheme = new DataScheme("Test");
        testScheme.AddAttribute(new AttributeDefinition(EXISTING_ATTRIBUTE_NAME, DataType.String));
    }
    
    [Test]
    public void Test_ConvertAttributeType_Small()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        string attributeName = "Field1";
        dataScheme.AddAttribute(new AttributeDefinition(attributeName, DataType.String));
        dataScheme.AddEntry(new DataEntry
        {
            { attributeName, "1" }
        });
        dataScheme.AddEntry(new DataEntry());
        
        // Act
        var conversionResponse = dataScheme.ConvertAttributeType(attributeName, DataType.Integer);
        
        // Assert
        Assert.IsTrue(conversionResponse.IsSuccess);
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

    [TestCase("Field", "", false)]
    [TestCase("Field", "Field", false)]
    [TestCase("Field", "OtherField", false)]
    [TestCase("Field", "Field2", true)]
    public void Test_UpdateAttributeName(string previousName, string newName, bool expected)
    {
        // Arrange
        testScheme.AddAttribute(new AttributeDefinition(previousName, DataType.String));
        testScheme.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { previousName, "FieldValue" }
        }));
        testScheme.AddAttribute(new AttributeDefinition("OtherField", DataType.String));
        testScheme.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { "OtherField", "FieldValue" }
        }));
        
        // Act
        var conversionResponse = testScheme.UpdateAttributeName(previousName, newName);
        
        Assert.That(conversionResponse.IsSuccess, Is.EqualTo(expected));
    }

    [TestCase("Field", "Field2")]
    public void Test_CreateNewEntry(params string[] attributeNames)
    {
        string defaultValue = "Bar";
        foreach (var attributeName in attributeNames)
        {
            testScheme.AddAttribute(new AttributeDefinition(attributeName, DataType.String, defaultValue: defaultValue));
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
        
        return addResponse.IsSuccess;
    }

    private static IEnumerable BadAttributes
    {
        get
        {
            yield return new TestCaseData(null).Returns(false);
            yield return new TestCaseData(new AttributeDefinition(EXISTING_ATTRIBUTE_NAME, DataType.String)).Returns(false);
        }
    }

    [Test]
    public void Test_SwapEntries()
    {
        var firstEntry = testScheme.CreateNewEntry();
        var secondEntry = testScheme.CreateNewEntry();

        var swapRes = testScheme.SwapEntries(0, 1);
        swapRes.AssertSuccess();
        
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
        moveRes.AssertSuccess();
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
        moveRes.AssertSuccess();
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
        Assert.That(res.IsSuccess, Is.EqualTo(expected));
    }
    
    [Test]
    public void Test_IncreaseAttributeRank()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        var firstAttribute = new AttributeDefinition("FirstAttribute", DataType.String);
        var secondAttribute = new AttributeDefinition("SecondAttribute", DataType.String);
        var thirdAttribute = new AttributeDefinition("ThirdAttribute", DataType.String);
        dataScheme.AddAttribute(firstAttribute);
        dataScheme.AddAttribute(secondAttribute);
        dataScheme.AddAttribute(thirdAttribute);
        
        // Act
        var increaseResponse = dataScheme.IncreaseAttributeRank(secondAttribute);
        
        // Assert
        Assert.IsTrue(increaseResponse.IsSuccess);
        Assert.That(dataScheme.GetAttribute(0), Is.EqualTo(secondAttribute));
        Assert.That(dataScheme.GetAttribute(1), Is.EqualTo(firstAttribute));
    }
    
    [Test]
    public void Test_DecreaseAttributeRank()
    {
        // Arrange
        var dataScheme = new DataScheme("Foo");
        var firstAttribute = new AttributeDefinition("FirstAttribute", DataType.String);
        var secondAttribute = new AttributeDefinition("SecondAttribute", DataType.String);
        var thirdAttribute = new AttributeDefinition("ThirdAttribute", DataType.String);
        dataScheme.AddAttribute(firstAttribute);
        dataScheme.AddAttribute(secondAttribute);
        dataScheme.AddAttribute(thirdAttribute);
        
        // Act
        var increaseResponse = dataScheme.DecreaseAttributeRank(firstAttribute);
        
        // Assert
        Assert.IsTrue(increaseResponse.IsSuccess);
        Assert.That(dataScheme.GetAttribute(0), Is.EqualTo(secondAttribute));
        Assert.That(dataScheme.GetAttribute(1), Is.EqualTo(firstAttribute));
    }

    [Test]
    public void Test_GetEntries()
    {
        var dataScheme = new DataScheme("Foo");
        var sortAttribute = "FirstAttribute";
        dataScheme.AddAttribute(new AttributeDefinition(sortAttribute, DataType.String));
        dataScheme.AddAttribute(new AttributeDefinition("SecondAttribute", DataType.String));

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
}