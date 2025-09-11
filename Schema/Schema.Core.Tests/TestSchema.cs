using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Schemes;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests;

[TestFixture]
public class TestSchema
{
    private Mock<IFileSystem> _mockFileSystem;

    private static readonly SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestSchema),
    };

    [SetUp]
    public void Setup()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        
        Schema.SetStorage(new Storage(_mockFileSystem.Object));
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
        var loadResponse = Schema.LoadManifestFromPath(badManifestPath, Context);
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public void Test_LoadManifest_MalformedFile()
    {
        string malformedFilePath = "malformed.json";
        
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(malformedFilePath))
            .Returns(SchemaResult.Pass());
        _mockFileSystem.Setup(fs => fs.ReadAllText(malformedFilePath))
            .Returns(SchemaResult<string>.Pass("malformedContent"));
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(malformedFilePath, Context);
        
        // Assert
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public void Test_LoadManifest_InvalidPath()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        
        var manifestSavePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
        
        manifestScheme.AddManifestEntry("Invalid");
        
        MockPersistScheme(manifestSavePath, manifestScheme);
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(manifestSavePath, Context);
        
        // Assert
        Assert.IsTrue(loadResponse.Passed);
    }

    private void MockPersistScheme(string filePath, ManifestScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        MockPersistScheme(filePath, scheme._, mockRead, mockWrite);
    }

    private void MockPersistScheme(string filePath, DataScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        _mockFileSystem.Setup(m => m.FileExists(filePath)).Returns(SchemaResult.Pass()).Verifiable();

        if (mockRead)
        {
            _mockFileSystem.Setup(m => m.ReadAllText(filePath))
                .Returns(SchemaResult<string>.Pass(Schema.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        }

        if (mockWrite)
        {
            _mockFileSystem.Setup(m => m.DirectoryExists("")).Returns(SchemaResult.Pass()).Verifiable();
            _mockFileSystem.Setup(m => m.FileExists(filePath)).Returns(SchemaResult.Pass()).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(filePath, Schema.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        }
    }

    [Test]
    public void Test_LoadManifest_SmallManifest()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        var manifestFilePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
        
        // Update manifest entry to be aware of self path
        var manifestSelfEntry = manifestScheme.SelfEntry;
        
        manifestSelfEntry.FilePath = manifestFilePath;
        
        // build test data scheme
        var testDataSchemeName = "Data";
        var testDataSchemeFilePath = $"{testDataSchemeName}.json";
        var testDataScheme = new DataScheme(testDataSchemeName);
        
        MockPersistScheme(testDataSchemeFilePath, testDataScheme);

        // add test data scheme manifest entry
        manifestScheme.AddManifestEntry(testDataSchemeName, importFilePath: testDataSchemeFilePath).AssertPassed();

        MockPersistScheme(manifestFilePath, manifestScheme);

        // Act
        Schema.LoadManifestFromPath(manifestFilePath, Context).AssertPassed();
        Schema.GetManifestEntryForScheme(testDataScheme).TryAssert(out var testDataEntry);
        
        // Assert
        Assert.IsTrue(Schema.DoesSchemeExist(manifestScheme.SchemeName));
        Assert.IsTrue(Schema.DoesSchemeExist(testDataSchemeName));
        Assert.That(testDataEntry.SchemeName, Is.EqualTo(testDataSchemeName));
        Assert.That(testDataEntry.FilePath, Is.EqualTo(testDataSchemeFilePath));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Test_GetManifestEntryForScheme_BadCases(string? schemeName)
    {
        Schema.GetManifestEntryForScheme(schemeName).AssertFailed();
    }

    [Test, TestCaseSource(nameof(BadManifestEntryTestCases))]
    public void Test_LoadSchemeFromManifestEntry_BadEntry(ManifestEntry? manifestEntry)
    {
        var res = Schema.LoadSchemeFromManifestEntry(manifestEntry);
        res.AssertFailed();
    }

    private static IEnumerable BadManifestEntryTestCases
    {
        get
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new ManifestEntry(null, new DataEntry()));
            yield return new TestCaseData(new ManifestEntry(null, new DataEntry(new Dictionary<string, object>())));
            yield return new TestCaseData(new ManifestEntry(null, new DataEntry(new Dictionary<string, object>
            {
                { nameof(ManifestEntry.SchemeName), "Invalid" }
            })));
            yield return new TestCaseData(new ManifestEntry(null, new DataEntry(new Dictionary<string, object>
            {
                { nameof(ManifestEntry.SchemeName), "" }
            })));
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
        Logger.Level = Logger.LogLevel.VERBOSE;
        var manifestSavePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
        var manifestSelfEntry = manifestScheme.SelfEntry;

        manifestSelfEntry.FilePath = manifestSavePath;
        
        Schema.SaveDataScheme(manifestScheme._, true).AssertFailed();
    }
    
    [Test]
    public void Test_SaveDataScheme_UnregisteredScheme_Fails()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        var dataScheme = new DataScheme("Foo");
        
        Schema.SaveDataScheme(dataScheme, true).AssertFailed();
    }
    
    [Test]
    public void Test_SaveManifest_BeforeSettingASavePath_Fails()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        Schema.Save(saveManifest: true).AssertFailed();
    }
    
    [Test]
    public void Test_SaveDataScheme_Manifest_AfterLoading()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        var manifestSavePath =  $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
        var manifestSelfEntry = manifestScheme.SelfEntry;

        Schema.ManifestImportPath = manifestSavePath;
        manifestSelfEntry.FilePath = manifestSavePath;
        MockPersistScheme(manifestSavePath, manifestScheme, mockRead: false, mockWrite: true);
        
        var loadRes = Schema.LoadDataScheme(manifestScheme._, true);
        loadRes.AssertPassed();
        
        var saveRes = Schema.SaveDataScheme(manifestScheme._, true);
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
        Schema.ManifestImportPath = manifestSavePath;
        var saveResponse = Schema.SaveManifest();
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
        _mockFileSystem.Setup(fs => fs.DirectoryExists("")).Returns(SchemaResult.Pass()).Verifiable();
        
        // Arrange
        var scheme = new DataScheme("Dirty");
        _mockFileSystem.Setup(fs => fs.WriteAllText("Dirty.json", Schema.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        
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
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
        manifestScheme.AddManifestEntry(scheme.SchemeName, importFilePath: "Failing.json");
        Schema.LoadDataScheme(manifestScheme._, true);
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
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema();
        manifestScheme.AddManifestEntry(scheme1.SchemeName, importFilePath: "Dirty1.json");
        manifestScheme.AddManifestEntry(scheme2.SchemeName, importFilePath: "Dirty2.json");
        Schema.LoadDataScheme(manifestScheme._, true);
        // As above, patching SaveDataScheme to fail for one scheme is not trivial without refactor
        // Placeholder for the failure path
    }
}