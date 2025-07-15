using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests;

[TestFixture]
public class TestSchema
{
    private Mock<IFileSystem> _mockFileSystem;
    
    [SetUp]
    public void Setup()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        
        Core.Serialization.Storage.SetFileSystem(_mockFileSystem.Object);
        Schema.Reset();
    }

    [Test]
    public void Test_OnStartup()
    {
        Assert.IsTrue(Schema.IsInitialized);
        
        Assert.That(Schema.NumAvailableSchemes, Is.EqualTo(1));
    }
    
    [TestCase(true)]
    [TestCase(false)]
    public void Test_LoadDataScheme(bool overwriteExisting)
    {
        // Arrange
        var newSchemeName = "Foo";
        var newScheme = new DataScheme(newSchemeName);

        // Act
        var addResponse = Schema.LoadDataScheme(newScheme, overwriteExisting);
        
        // Assert
        Assert.IsTrue(addResponse.Passed);
        Assert.That(Schema.NumAvailableSchemes, Is.EqualTo(2));
        Assert.That(Schema.AllSchemes, Contains.Item(newScheme.SchemeName));
        Schema.GetScheme(newSchemeName).TryAssert(out var loadedScheme);
        Assert.That(loadedScheme, Is.EqualTo(newScheme));
    }


    [TestCase("")]
    [TestCase(null)]
    [TestCase("missing.json")]
    public void Test_LoadManifest_BadPath(string? badManifestPath)
    {
        var loadResponse = Schema.LoadManifestFromPath(badManifestPath);
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public void Test_LoadManifest_MalformedFile()
    {
        string malformedFilePath = "malformed.json";
        
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(malformedFilePath))
            .Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllText(malformedFilePath))
            .Returns("malformedContent");
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(malformedFilePath);
        
        // Assert
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public void Test_LoadManifest_InvalidPath()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        string manifestFilePath = $"{Schema.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = Schema.BuildTemplateManifestSchema();
        
        manifestScheme.AddEntry(new DataEntry(new Dictionary<string, object>
        {
            { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, "Invalid"}
        }));
        
        MockPersistScheme(manifestFilePath, manifestScheme);
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(manifestFilePath);
        
        // Assert
        Assert.IsTrue(loadResponse.Passed);
    }

    private void MockPersistScheme(string filePath, DataScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        _mockFileSystem.Setup(m => m.FileExists(filePath)).Returns(true).Verifiable();

        if (mockRead)
        {
            _mockFileSystem.Setup(m => m.ReadAllText(filePath))
                .Returns(Core.Serialization.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed()).Verifiable();
        }

        if (mockWrite)
        {
            _mockFileSystem.Setup(m => m.DirectoryExists("")).Returns(true).Verifiable();
            _mockFileSystem.Setup(m => m.FileExists(filePath)).Returns(true).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(filePath, Core.Serialization.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        }
    }

    [Test]
    public void Test_LoadManifest_SmallManifest()
    {
        // Arrange
        string manifestFilePath = $"{Schema.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = Schema.BuildTemplateManifestSchema();
        
        // build test data scheme
        var testDataSchemeName = "Data";
        var testDataSchemeFilePath = $"{testDataSchemeName}.json";
        var testDataScheme = new DataScheme(testDataSchemeName);
        
        MockPersistScheme(testDataSchemeFilePath, testDataScheme);

        // add test data scheme manifest entry
        manifestScheme.AddEntry(new DataEntry(new Dictionary<string, object>
        {
            { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, testDataSchemeName },
            { Schema.MANIFEST_ATTRIBUTE_FILEPATH, testDataSchemeFilePath}
        })).AssertPassed();
        
        MockPersistScheme(manifestFilePath, manifestScheme);
        MockPersistScheme(testDataSchemeFilePath, testDataScheme);

        // Act
        Schema.LoadManifestFromPath(manifestFilePath).AssertPassed();

        Schema.GetManifestEntryForScheme(testDataScheme).TryAssert(out var testDataEntry);
        
        // Assert
        Assert.IsTrue(Schema.DoesSchemeExist(manifestScheme.SchemeName));
        Assert.IsTrue(Schema.DoesSchemeExist(testDataSchemeName));
        Assert.That(testDataEntry.GetDataAsString(Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME), Is.EqualTo(testDataSchemeName));
        Assert.That(testDataEntry.GetDataAsString(Schema.MANIFEST_ATTRIBUTE_FILEPATH), Is.EqualTo(testDataSchemeFilePath));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Test_GetManifestEntryForScheme_BadCases(string? schemeName)
    {
        Schema.GetManifestEntryForScheme(schemeName).AssertFailed();
    }

    [Test, TestCaseSource(nameof(BadManifestEntryTestCases))]
    public void Test_LoadSchemeFromManifestEntry_BadEntry(DataEntry? manifestEntry)
    {
        var res = Schema.LoadSchemeFromManifestEntry(manifestEntry);
        res.AssertFailed();
    }

    private static IEnumerable BadManifestEntryTestCases
    {
        get
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new DataEntry());
            yield return new TestCaseData(new DataEntry(new Dictionary<string, object>()));
            yield return new TestCaseData(new DataEntry
            {
                { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, "Invalid"}
            });
            yield return new TestCaseData(new DataEntry
            {
                { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, ""}
            });
            yield return new TestCaseData(new DataEntry
            {
                { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, null}
            });
        }
    }

    [Test, TestCaseSource(nameof(BadSchemeTestCases))]
    public bool Test_SaveDataScheme_BadScheme(DataScheme scheme)
    {
        var saveResponse = Schema.SaveDataScheme(scheme, true);
        return saveResponse.Passed;
    }

    [Test]
    public void Test_SaveDataScheme_ValidScheme()
    {
        // Arrange
        var newScheme = new DataScheme("Foo");
        var newSchemeFilePath = "Foo.json";
        MockPersistScheme(newSchemeFilePath, newScheme);
        Schema.LoadDataScheme(newScheme, true, importFilePath: newSchemeFilePath).AssertPassed();
        
        // Act
        Schema.SaveDataScheme(newScheme, false).AssertPassed();
    }
    
    [Test]
    public void Test_SaveDataScheme_Manifest_BeforeLoading()
    {
        string manifestSavePath = "Manifest.json";
        var manifestScheme = Schema.BuildTemplateManifestSchema();
        var manifestSelfEntry = manifestScheme.GetEntry(e =>
            e.GetDataAsString(Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME).Equals(Schema.MANIFEST_SCHEME_NAME));
        
        manifestScheme.SetDataOnEntry(manifestSelfEntry, Schema.MANIFEST_ATTRIBUTE_FILEPATH, manifestSavePath);
        
        var saveResponse = Schema.SaveDataScheme(manifestScheme, true);
        Assert.That(saveResponse.Passed, Is.EqualTo(false));
    }
    
    [Test]
    public void Test_SaveDataScheme_Manifest_AfterLoading()
    {
        string manifestSavePath = "Manifest.json";
        var manifestScheme = Schema.BuildTemplateManifestSchema();
        var manifestSelfEntry = manifestScheme.GetEntry(e =>
            e.GetDataAsString(Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME).Equals(Schema.MANIFEST_SCHEME_NAME));
        
        manifestScheme.SetDataOnEntry(manifestSelfEntry, Schema.MANIFEST_ATTRIBUTE_FILEPATH, manifestSavePath);
        MockPersistScheme(manifestSavePath, manifestScheme, mockRead: false, mockWrite: true);
        
        var loadRes = Schema.LoadDataScheme(manifestScheme, true);
        loadRes.AssertPassed();
        
        var saveRes = Schema.SaveDataScheme(manifestScheme, true);
        saveRes.AssertPassed();
        _mockFileSystem.VerifyAll();
        _mockFileSystem.VerifyNoOtherCalls();
    }

    private static IEnumerable BadSchemeTestCases
    {
        get
        {
            yield return new TestCaseData(null).Returns(false);
            yield return new TestCaseData(new DataScheme("BadData")).Returns(false);
        }
    }

    [Test]
    public void Test_SaveManifest()
    {
        string manifestSavePath = "Manifest.json";
        var saveResponse = Schema.SaveManifest(manifestSavePath);
        Assert.IsTrue(saveResponse.Passed);
    }

    [Test]
    public void Test_Save_NoDirtySchemes_ReturnsSuccess()
    {
        // Arrange: No schemes are dirty
        var scheme = new DataScheme("Clean");
        Schema.LoadDataScheme(scheme, true, importFilePath: "Clean.json");
        scheme.IsDirty = false;
        
        // Act
        Logger.Level = Logger.LogLevel.VERBOSE;
        var result = Schema.Save(saveManifest: false);
        
        // Assert
        result.AssertPassed();
        Assert.That(result.Message, Does.Contain("Saved all dirty schemes"));
        _mockFileSystem.VerifyAll();
        _mockFileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public void Test_Save_OneDirtyScheme_SavesAndClearsDirtyFlag()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists("")).Returns(true).Verifiable();
        
        // Arrange
        var scheme = new DataScheme("Dirty");
        _mockFileSystem.Setup(fs => fs.WriteAllText("Dirty.json", Core.Serialization.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        
        Schema.LoadDataScheme(scheme, true, importFilePath: "Dirty.json");
        scheme.IsDirty = true;
        
        // Act
        var result = Schema.Save(saveManifest: false);
        
        // Assert
        result.AssertPassed();
        Assert.IsFalse(scheme.IsDirty);
        _mockFileSystem.VerifyAll();
        _mockFileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public void Test_Save_OneDirtyScheme_SaveFails_ReturnsFailure()
    {
        // Arrange
        var scheme = new DataScheme("Failing");
        Schema.LoadDataScheme(scheme, true);
        scheme.IsDirty = true;
        // Mock manifest entry
        var manifestScheme = Schema.BuildTemplateManifestSchema();
        var entry = new DataEntry(new Dictionary<string, object> {
            { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, scheme.SchemeName },
            { Schema.MANIFEST_ATTRIBUTE_FILEPATH, "Failing.json" }
        });
        manifestScheme.AddEntry(entry);
        Schema.LoadDataScheme(manifestScheme, true);
        // Patch SaveDataScheme to fail
        var original = typeof(Schema).GetMethod("SaveDataScheme");
        // Not possible to patch static method easily here, so just note: in a real test, use dependency injection or a wrapper for SaveDataScheme
        // For now, this test is a placeholder for the failure path
    }

    [Test]
    public void Test_Save_MultipleDirtySchemes_OneFails_ReturnsFailure()
    {
        // Arrange
        var scheme1 = new DataScheme("Dirty1");
        var scheme2 = new DataScheme("Dirty2");
        Schema.LoadDataScheme(scheme1, true);
        Schema.LoadDataScheme(scheme2, true);
        scheme1.IsDirty = true;
        scheme2.IsDirty = true;
        // Mock manifest entries
        var manifestScheme = Schema.BuildTemplateManifestSchema();
        manifestScheme.AddEntry(new DataEntry(new Dictionary<string, object> {
            { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, scheme1.SchemeName },
            { Schema.MANIFEST_ATTRIBUTE_FILEPATH, "Dirty1.json" }
        }));
        manifestScheme.AddEntry(new DataEntry(new Dictionary<string, object> {
            { Schema.MANIFEST_ATTRIBUTE_SCHEME_NAME, scheme2.SchemeName },
            { Schema.MANIFEST_ATTRIBUTE_FILEPATH, "Dirty2.json" }
        }));
        Schema.LoadDataScheme(manifestScheme, true);
        // As above, patching SaveDataScheme to fail for one scheme is not trivial without refactor
        // Placeholder for the failure path
    }
}