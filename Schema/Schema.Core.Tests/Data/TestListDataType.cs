using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Schema.Core.Data;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestListDataType
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestListDataType)
    };

    #region Constructor Tests

    [Test]
    public void Constructor_Default_CreatesEmptyList()
    {
        var listType = new ListDataType();
        
        Assert.That(listType.ElementType, Is.Null);
        Assert.That(listType.DefaultValue, Is.Not.Null);
        Assert.That(listType.TypeName, Is.EqualTo("List"));
    }

    [Test]
    public void Constructor_WithIntegerType_CreatesIntegerList()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        
        Assert.That(listType.ElementType, Is.EqualTo(DataType.Integer));
        Assert.That(listType.TypeName, Is.EqualTo("List of Integer"));
        Assert.That(listType.DefaultValue, Is.TypeOf<int[]>());
        Assert.That(((int[])listType.DefaultValue).Length, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_WithStringType_CreatesStringList()
    {
        var listType = new ListDataType(DataType.Text, Context);
        
        Assert.That(listType.ElementType, Is.EqualTo(DataType.Text));
        Assert.That(listType.TypeName, Is.EqualTo("List of String"));
        Assert.That(listType.DefaultValue, Is.TypeOf<string[]>());
    }

    [Test]
    public void Constructor_WithBooleanType_CreatesBooleanList()
    {
        var listType = new ListDataType(DataType.Boolean, Context);
        
        Assert.That(listType.ElementType, Is.EqualTo(DataType.Boolean));
        Assert.That(listType.TypeName, Is.EqualTo("List of Boolean"));
        Assert.That(listType.DefaultValue, Is.TypeOf<bool[]>());
    }

    [Test]
    public void Constructor_WithFloatType_CreatesFloatList()
    {
        var listType = new ListDataType(DataType.Float, Context);
        
        Assert.That(listType.ElementType, Is.EqualTo(DataType.Float));
        Assert.That(listType.TypeName, Is.EqualTo("List of Float"));
        Assert.That(listType.DefaultValue, Is.TypeOf<float[]>());
    }

    [Test]
    public void Constructor_WithDateTimeType_CreatesDateTimeList()
    {
        var listType = new ListDataType(DataType.DateTime, Context);
        
        Assert.That(listType.ElementType, Is.EqualTo(DataType.DateTime));
        Assert.That(listType.TypeName, Is.EqualTo("List of Date Time"));
        Assert.That(listType.DefaultValue, Is.TypeOf<DateTime[]>());
    }

    [Test]
    public void Constructor_WithGuidType_CreatesGuidList()
    {
        var listType = new ListDataType(DataType.Guid, Context);
        
        Assert.That(listType.ElementType, Is.EqualTo(DataType.Guid));
        Assert.That(listType.TypeName, Is.EqualTo("List of Guid"));
        Assert.That(listType.DefaultValue, Is.TypeOf<Guid[]>());
    }

    #endregion

    #region IsValidValue Tests

    [Test]
    public void IsValidValue_NullValue_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, null);
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("null"));
    }

    [Test]
    public void IsValidValue_StringValue_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, "not a list");
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("string"));
    }

    [Test]
    public void IsValidValue_NonEnumerableValue_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, 123);
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("enumerable"));
    }

    [Test]
    public void IsValidValue_EmptyIntArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, new int[] { });
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_ValidIntArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, new int[] { 1, 2, 3 });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Message, Does.Contain("3 elements"));
    }

    [Test]
    public void IsValidValue_ValidStringArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Text, Context);
        var result = listType.IsValidValue(Context, new string[] { "a", "b", "c" });
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_ValidBoolArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Boolean, Context);
        var result = listType.IsValidValue(Context, new bool[] { true, false, true });
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_ValidFloatArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Float, Context);
        var result = listType.IsValidValue(Context, new float[] { 1.5f, 2.5f, 3.5f });
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_ValidDateTimeArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.DateTime, Context);
        var dates = new DateTime[] { DateTime.Now, DateTime.UtcNow };
        var result = listType.IsValidValue(Context, dates);
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_ValidGuidArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Guid, Context);
        var guids = new Guid[] { Guid.NewGuid(), Guid.NewGuid() };
        var result = listType.IsValidValue(Context, guids);
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_MixedTypesInIntList_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, new object[] { 1, "two", 3 });
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("index 1"));
    }

    [Test]
    public void IsValidValue_WrongTypeArray_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, new string[] { "1", "2", "3" });
        
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void IsValidValue_ListWithoutElementType_ReturnsTrue()
    {
        var listType = new ListDataType();
        var result = listType.IsValidValue(Context, new object[] { 1, "two", true });
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void IsValidValue_IEnumerableOfInts_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var list = new List<int> { 1, 2, 3 };
        var result = listType.IsValidValue(Context, list);
        
        Assert.That(result.Passed, Is.True);
    }

    #endregion

    #region ConvertValue Tests

    [Test]
    public void ConvertValue_NullValue_ReturnsDefaultValue()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, null);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
        Assert.That(((int[])result.Result).Length, Is.EqualTo(0));
    }

    [Test]
    public void ConvertValue_StringValue_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, "not a list");
        
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void ConvertValue_SingleStringToStringList_ConvertsToArray()
    {
        // User convenience feature: automatically wrap a single string in a list
        var listType = new ListDataType(DataType.Text, Context);
        var result = listType.ConvertValue(Context, "single value");
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<string[]>());
        var array = (string[])result.Result;
        Assert.That(array.Length, Is.EqualTo(1));
        Assert.That(array[0], Is.EqualTo("single value"));
    }

    [Test]
    public void ConvertValue_NonEnumerableValue_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, 123);
        
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void ConvertValue_IntArrayToIntList_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, new int[] { 1, 2, 3 });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
        var array = (int[])result.Result;
        Assert.That(array, Is.EqualTo(new int[] { 1, 2, 3 }));
    }

    [Test]
    public void ConvertValue_StringArrayToStringList_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Text, Context);
        var result = listType.ConvertValue(Context, new string[] { "a", "b", "c" });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<string[]>());
        var array = (string[])result.Result;
        Assert.That(array, Is.EqualTo(new string[] { "a", "b", "c" }));
    }

    [Test]
    public void ConvertValue_StringArrayToIntList_ConvertsElements()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, new string[] { "1", "2", "3" });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
        var array = (int[])result.Result;
        Assert.That(array, Is.EqualTo(new int[] { 1, 2, 3 }));
    }

    [Test]
    public void ConvertValue_MixedObjectArrayToIntList_ConvertsElements()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, new object[] { 1, "2", 3L });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
        var array = (int[])result.Result;
        Assert.That(array, Is.EqualTo(new int[] { 1, 2, 3 }));
    }

    [Test]
    public void ConvertValue_InvalidStringInIntList_ReturnsFalse()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, new string[] { "1", "not a number", "3" });
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("index 1"));
    }

    [Test]
    public void ConvertValue_EmptyArray_ReturnsEmptyList()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.ConvertValue(Context, new int[] { });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
        Assert.That(((int[])result.Result).Length, Is.EqualTo(0));
    }

    [Test]
    public void ConvertValue_BoolArrayToBoolList_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Boolean, Context);
        var result = listType.ConvertValue(Context, new bool[] { true, false, true });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<bool[]>());
        var array = (bool[])result.Result;
        Assert.That(array, Is.EqualTo(new bool[] { true, false, true }));
    }

    [Test]
    public void ConvertValue_StringArrayToBoolList_ConvertsElements()
    {
        var listType = new ListDataType(DataType.Boolean, Context);
        var result = listType.ConvertValue(Context, new string[] { "true", "false", "true" });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<bool[]>());
    }

    [Test]
    public void ConvertValue_FloatArrayToFloatList_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Float, Context);
        var result = listType.ConvertValue(Context, new float[] { 1.5f, 2.5f, 3.5f });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<float[]>());
    }

    [Test]
    public void ConvertValue_IntArrayToFloatList_ConvertsElements()
    {
        var listType = new ListDataType(DataType.Float, Context);
        var result = listType.ConvertValue(Context, new int[] { 1, 2, 3 });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<float[]>());
    }

    [Test]
    public void ConvertValue_JArrayToIntList_ConvertsFromJson()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var jArray = JArray.Parse("[1, 2, 3]");
        var result = listType.ConvertValue(Context, jArray);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
        var array = (int[])result.Result;
        Assert.That(array, Is.EqualTo(new int[] { 1, 2, 3 }));
    }

    [Test]
    public void ConvertValue_JArrayToStringList_ConvertsFromJson()
    {
        var listType = new ListDataType(DataType.Text, Context);
        var jArray = JArray.Parse("[\"a\", \"b\", \"c\"]");
        var result = listType.ConvertValue(Context, jArray);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<string[]>());
        var array = (string[])result.Result;
        Assert.That(array, Is.EqualTo(new string[] { "a", "b", "c" }));
    }

    [Test]
    public void ConvertValue_NoElementType_ConvertsToObjectArray()
    {
        var listType = new ListDataType();
        var result = listType.ConvertValue(Context, new object[] { 1, "two", true });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<object[]>());
        var array = (object[])result.Result;
        Assert.That(array.Length, Is.EqualTo(3));
    }

    [Test]
    public void ConvertValue_ListToArray_Converts()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var list = new List<int> { 1, 2, 3 };
        var result = listType.ConvertValue(Context, list);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<int[]>());
    }

    #endregion

    #region Clone Tests

    [Test]
    public void Clone_CreatesNewInstance()
    {
        var original = new ListDataType(DataType.Integer, Context);
        var cloned = (ListDataType)original.Clone();
        
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned, Is.Not.SameAs(original));
    }

    [Test]
    public void Clone_CopiesElementType()
    {
        var original = new ListDataType(DataType.Integer, Context);
        var cloned = (ListDataType)original.Clone();
        
        Assert.That(cloned.ElementType, Is.EqualTo(original.ElementType));
    }

    [Test]
    public void Clone_CopiesDefaultValue()
    {
        var original = new ListDataType(DataType.Integer, Context);
        var cloned = (ListDataType)original.Clone();
        
        Assert.That(cloned.DefaultValue, Is.EqualTo(original.DefaultValue));
    }

    [Test]
    public void Clone_WithoutElementType_Works()
    {
        var original = new ListDataType();
        var cloned = (ListDataType)original.Clone();
        
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.ElementType, Is.Null);
    }

    #endregion

    #region Equality Tests

    [Test]
    public void Equals_SameElementType_ReturnsTrue()
    {
        var list1 = new ListDataType(DataType.Integer, Context);
        var list2 = new ListDataType(DataType.Integer, Context);
        
        Assert.That(list1.Equals(list2), Is.True);
        Assert.That(list1 == list2, Is.True);
    }

    [Test]
    public void Equals_DifferentElementType_ReturnsFalse()
    {
        var list1 = new ListDataType(DataType.Integer, Context);
        var list2 = new ListDataType(DataType.Text, Context);
        
        Assert.That(list1.Equals(list2), Is.False);
        Assert.That(list1 == list2, Is.False);
    }

    [Test]
    public void Equals_BothNoElementType_ReturnsTrue()
    {
        var list1 = new ListDataType();
        var list2 = new ListDataType();
        
        Assert.That(list1.Equals(list2), Is.True);
    }

    [Test]
    public void Equals_OneWithElementTypeOneWithout_ReturnsFalse()
    {
        var list1 = new ListDataType(DataType.Integer, Context);
        var list2 = new ListDataType();
        
        Assert.That(list1.Equals(list2), Is.False);
    }

    [Test]
    public void Equals_Null_ReturnsFalse()
    {
        var list1 = new ListDataType(DataType.Integer, Context);
        
        Assert.That(list1.Equals(null), Is.False);
    }

    [Test]
    public void GetHashCode_SameElementType_SameHashCode()
    {
        var list1 = new ListDataType(DataType.Integer, Context);
        var list2 = new ListDataType(DataType.Integer, Context);
        
        Assert.That(list1.GetHashCode(), Is.EqualTo(list2.GetHashCode()));
    }

    #endregion

    #region GetDataMethod Tests

    [Test]
    public void GetDataMethod_IntegerList_ReturnsCorrectMethod()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var attribute = new AttributeDefinition(null, "testAttr", listType);
        var result = listType.GetDataMethod(Context, attribute);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Does.Contain("GetDataAsList"));
        Assert.That(result.Result, Does.Contain("testAttr"));
    }

    [Test]
    public void GetDataMethod_StringList_ReturnsCorrectMethod()
    {
        var listType = new ListDataType(DataType.Text, Context);
        var attribute = new AttributeDefinition(null, "testAttr", listType);
        var result = listType.GetDataMethod(Context, attribute);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Does.Contain("GetDataAsList"));
        Assert.That(result.Result, Does.Contain("testAttr"));
    }

    [Test]
    public void GetDataMethod_BoolList_ReturnsCorrectMethod()
    {
        var listType = new ListDataType(DataType.Boolean, Context);
        var attribute = new AttributeDefinition(null, "testAttr", listType);
        var result = listType.GetDataMethod(Context, attribute);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Does.Contain("GetDataAsList"));
        Assert.That(result.Result, Does.Contain("testAttr"));
    }

    [Test]
    public void GetDataMethod_FloatList_ReturnsCorrectMethod()
    {
        var listType = new ListDataType(DataType.Float, Context);
        var attribute = new AttributeDefinition(null, "testAttr", listType);
        var result = listType.GetDataMethod(Context, attribute);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Does.Contain("GetDataAsList"));
        Assert.That(result.Result, Does.Contain("testAttr"));
    }

    #endregion

    #region CSDataType Tests

    [Test]
    public void CSDataType_IntegerList_ReturnsCorrectType()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        
        Assert.That(listType.CSDataType, Does.Contain("List"));
        Assert.That(listType.CSDataType, Does.Contain(DataType.Integer.CSDataType));
    }

    [Test]
    public void CSDataType_StringList_ReturnsCorrectType()
    {
        var listType = new ListDataType(DataType.Text, Context);
        
        Assert.That(listType.CSDataType, Does.Contain("List"));
        Assert.That(listType.CSDataType, Does.Contain(DataType.Text.CSDataType));
    }

    [Test]
    public void CSDataType_BoolList_ReturnsCorrectType()
    {
        var listType = new ListDataType(DataType.Boolean, Context);
        
        Assert.That(listType.CSDataType, Does.Contain("List"));
        Assert.That(listType.CSDataType, Does.Contain(DataType.Boolean.CSDataType));
    }

    [Test]
    public void CSDataType_FloatList_ReturnsCorrectType()
    {
        var listType = new ListDataType(DataType.Float, Context);
        
        Assert.That(listType.CSDataType, Does.Contain("List"));
        Assert.That(listType.CSDataType, Does.Contain(DataType.Float.CSDataType));
    }

    [Test]
    public void CSDataType_DateTimeList_ReturnsCorrectType()
    {
        var listType = new ListDataType(DataType.DateTime, Context);
        
        Assert.That(listType.CSDataType, Does.Contain("List"));
        Assert.That(listType.CSDataType, Does.Contain(DataType.DateTime.CSDataType));
    }

    [Test]
    public void CSDataType_GuidList_ReturnsCorrectType()
    {
        var listType = new ListDataType(DataType.Guid, Context);
        
        Assert.That(listType.CSDataType, Does.Contain("List"));
        Assert.That(listType.CSDataType, Does.Contain(DataType.Guid.CSDataType));
    }

    [Test]
    public void CSDataType_NoElementType_ReturnsObjectList()
    {
        var listType = new ListDataType();
        
        Assert.That(listType.CSDataType, Is.EqualTo("System.Collections.Generic.List<object>"));
    }

    #endregion

    #region TypeName Tests

    [Test]
    public void TypeName_IntegerList_ReturnsCorrectName()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        
        Assert.That(listType.TypeName, Is.EqualTo("List of Integer"));
    }

    [Test]
    public void TypeName_StringList_ReturnsCorrectName()
    {
        var listType = new ListDataType(DataType.Text, Context);
        
        Assert.That(listType.TypeName, Is.EqualTo("List of String"));
    }

    [Test]
    public void TypeName_NoElementType_ReturnsGenericName()
    {
        var listType = new ListDataType();
        
        Assert.That(listType.TypeName, Is.EqualTo("List"));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void IsValidValue_NestedArrays_HandlesCorrectly()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        // Arrays of arrays should fail since elements aren't integers
        var result = listType.IsValidValue(Context, new object[] { new int[] { 1, 2 }, new int[] { 3, 4 } });
        
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void ConvertValue_LargeArray_Handles()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var largeArray = Enumerable.Range(0, 1000).ToArray();
        var result = listType.ConvertValue(Context, largeArray);
        
        Assert.That(result.Passed, Is.True);
        var converted = (int[])result.Result;
        Assert.That(converted.Length, Is.EqualTo(1000));
    }

    [Test]
    public void IsValidValue_SingleElementArray_ReturnsTrue()
    {
        var listType = new ListDataType(DataType.Integer, Context);
        var result = listType.IsValidValue(Context, new int[] { 42 });
        
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void ConvertValue_DoubleArrayToFloatList_Converts()
    {
        var listType = new ListDataType(DataType.Float, Context);
        var result = listType.ConvertValue(Context, new double[] { 1.5, 2.5, 3.5 });
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.TypeOf<float[]>());
    }

    #endregion
}

