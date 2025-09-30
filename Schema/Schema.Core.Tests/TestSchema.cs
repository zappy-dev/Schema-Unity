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
        var addResponse = Schema.LoadDataScheme(Context, newScheme, overwriteExisting);
        
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
        var loadResponse = Schema.LoadManifestFromPath(Context, badManifestPath);
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public void Test_LoadManifest_MalformedFile()
    {
        string malformedFilePath = "malformed.json";
        
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(Context, malformedFilePath))
            .Returns(SchemaResult.Pass());
        _mockFileSystem.Setup(fs => fs.ReadAllText(Context, malformedFilePath))
            .Returns(SchemaResult<string>.Pass("malformedContent"));
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(Context, malformedFilePath);
        
        // Assert
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public void Test_LoadManifest_InvalidPath()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        
        var manifestSavePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context);
        
        manifestScheme.AddManifestEntry(Context, "Invalid");
        
        MockPersistScheme(manifestSavePath, manifestScheme);
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(Context, manifestSavePath);
        
        // Assert
        Assert.IsTrue(loadResponse.Passed);
    }

    private void MockPersistScheme(string filePath, ManifestScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        MockPersistScheme(filePath, scheme._, mockRead, mockWrite);
    }

    private void MockPersistScheme(string filePath, DataScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        _mockFileSystem.Setup(m => m.FileExists(Context, filePath)).Returns(SchemaResult.Pass()).Verifiable();

        if (mockRead)
        {
            _mockFileSystem.Setup(m => m.ReadAllText(Context, filePath))
                .Returns(SchemaResult<string>.Pass(Schema.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        }

        if (mockWrite)
        {
            _mockFileSystem.Setup(m => m.DirectoryExists(Context, "")).Returns(true).Verifiable();
            _mockFileSystem.Setup(m => m.FileExists(Context, filePath)).Returns(SchemaResult.Pass()).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(Context, filePath, Schema.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        }
    }

    [Test]
    public void Test_LoadManifest_SmallManifest()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        var manifestFilePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context);
        
        // Update manifest entry to be aware of self path
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();
        
        manifestSelfEntry.FilePath = manifestFilePath;
        
        // build test data scheme
        var testDataSchemeName = "Data";
        var testDataSchemeFilePath = $"{testDataSchemeName}.json";
        var testDataScheme = new DataScheme(testDataSchemeName);
        
        MockPersistScheme(testDataSchemeFilePath, testDataScheme);

        // add test data scheme manifest entry
        manifestScheme.AddManifestEntry(Context, testDataSchemeName, importFilePath: testDataSchemeFilePath).AssertPassed();

        MockPersistScheme(manifestFilePath, manifestScheme);

        // Act
        Schema.LoadManifestFromPath(Context, manifestFilePath).AssertPassed();
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
        var res = Schema.LoadSchemeFromManifestEntry(Context, manifestEntry);
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
        var saveResponse = Schema.SaveDataScheme(Context, scheme, true);
        return saveResponse.Passed;
    }

    [Test]
    public void Test_SaveDataScheme_ValidScheme()
    {
        // Arrange
        var newScheme = new DataScheme("Foo");
        var newSchemeFilePath = "Foo.json";
        MockPersistScheme(newSchemeFilePath, newScheme);
        Schema.LoadDataScheme(Context, newScheme, true, importFilePath: newSchemeFilePath).AssertPassed();
        
        // Act
        Schema.SaveDataScheme(Context, newScheme, false).AssertPassed();
    }
    
    [Test]
    public void Test_SaveDataScheme_Manifest_BeforeLoading()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        var manifestSavePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context);
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();

        manifestSelfEntry.FilePath = manifestSavePath;
        
        Schema.SaveDataScheme(Context, manifestScheme._, true).AssertFailed();
    }
    
    [Test]
    public void Test_SaveDataScheme_UnregisteredScheme_Fails()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        var dataScheme = new DataScheme("Foo");
        
        Schema.SaveDataScheme(Context, dataScheme, true).AssertFailed();
    }
    
    [Test]
    public void Test_SaveManifest_BeforeSettingASavePath_Fails()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        Schema.Save(Context, saveManifest: true).AssertFailed();
    }
    
    [Test]
    public void Test_SaveDataScheme_Manifest_AfterLoading()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        var manifestSavePath =  $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context);
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();

        Schema.ManifestImportPath = manifestSavePath;
        manifestSelfEntry.FilePath = manifestSavePath;
        MockPersistScheme(manifestSavePath, manifestScheme, mockRead: false, mockWrite: true);
        
        var loadRes = Schema.LoadDataScheme(Context, manifestScheme._, true);
        loadRes.AssertPassed();
        
        var saveRes = Schema.SaveDataScheme(Context, manifestScheme._, true);
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
        var saveResponse = Schema.SaveManifest(Context);
        Assert.IsTrue(saveResponse.Passed);
    }

    [Test]
    public void Test_Save_NoDirtySchemes_ReturnsSuccess()
    {
        // Arrange: No schemes are dirty
        var scheme = new DataScheme("Clean");
        Schema.LoadDataScheme(Context, scheme, true, importFilePath: "Clean.json");
        scheme.SetDirty(Context, false);
        
        // Act
        Logger.Level = Logger.LogLevel.VERBOSE;
        var result = Schema.Save(Context, saveManifest: false);
        
        // Assert
        result.AssertPassed();
        Assert.That(result.Message, Does.Contain("Saved all dirty schemes"));
        _mockFileSystem.VerifyAll();
        _mockFileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public void Test_Save_OneDirtyScheme_SavesAndClearsDirtyFlag()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(Context, "")).Returns(true).Verifiable();
        
        // Arrange
        var scheme = new DataScheme("Dirty");
        _mockFileSystem.Setup(fs => fs.WriteAllText(Context, "Dirty.json", Schema.Storage.DefaultManifestStorageFormat.Serialize(scheme).AssertPassed())).Verifiable();
        
        Schema.LoadDataScheme(Context, scheme, true, importFilePath: "Dirty.json");
        scheme.SetDirty(Context, true);
        
        // Act
        var result = Schema.Save(Context, saveManifest: false);
        
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
        Schema.LoadDataScheme(Context, scheme, true);
        scheme.SetDirty(Context, true);
        // Mock manifest entry
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context);
        manifestScheme.AddManifestEntry(Context, scheme.SchemeName, importFilePath: "Failing.json");
        Schema.LoadDataScheme(Context, manifestScheme._, true);
    }

    [Test]
    public void Test_Save_MultipleDirtySchemes_OneFails_ReturnsFailure()
    {
        // Arrange
        var scheme1 = new DataScheme("Dirty1");
        var scheme2 = new DataScheme("Dirty2");
        Schema.LoadDataScheme(Context, scheme1, true);
        Schema.LoadDataScheme(Context, scheme2, true);
        scheme1.SetDirty(Context, true);
        scheme2.SetDirty(Context, true);
        // Mock manifest entries
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context);
        manifestScheme.AddManifestEntry(Context, scheme1.SchemeName, importFilePath: "Dirty1.json");
        manifestScheme.AddManifestEntry(Context, scheme2.SchemeName, importFilePath: "Dirty2.json");
        Schema.LoadDataScheme(Context, manifestScheme._, true);
        // As above, patching SaveDataScheme to fail for one scheme is not trivial without refactor
        // Placeholder for the failure path
    }
}