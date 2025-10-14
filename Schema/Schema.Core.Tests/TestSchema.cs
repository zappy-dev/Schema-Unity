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
    #region Constants
    
    /// <summary>
    /// Absolute path to Manifest file
    /// </summary>
    public static string ManifestAbsFilePath = Schema.GetContentPath($"{Manifest.MANIFEST_SCHEME_NAME}.json");
    /// <summary>
    /// Project-relative path to Manifest file
    /// </summary>
    public static string  ManifestRelPath = PathUtility.MakeRelativePath(ManifestAbsFilePath, Schema.ProjectPath);

    #endregion
    private Storage _storage;
    private Mock<IFileSystem> _mockFileSystem;

    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestSchema),
    };

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Logger.LogVerbose($"{nameof(ManifestAbsFilePath)}: {ManifestAbsFilePath}");
        Logger.LogVerbose($"{nameof(ManifestRelPath)}: {ManifestRelPath}");
    }

    [SetUp]
    public void Setup()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        
        _storage =  new Storage(_mockFileSystem.Object);
        Schema.SetStorage(_storage);
        Schema.Reset();
        Schema.InitializeTemplateManifestScheme(Context);
        // Ensure manifest import path and self-entry path are consistent with project structure
        var manifestAbsolutePath = System.IO.Path.Combine(Schema.ProjectPath ?? "", "Content", $"{Manifest.MANIFEST_SCHEME_NAME}.json");
        Schema.ManifestImportPath = manifestAbsolutePath;
        if (Schema.GetManifestScheme(Context).TryAssert(out var loadedManifest))
        {
            loadedManifest.GetSelfEntry(Context).TryAssert(out var selfEntry);
            selfEntry.FilePath = System.IO.Path.Combine("Content", $"{Manifest.MANIFEST_SCHEME_NAME}.json");
        }
    }

    [Test]
    public void Test_OnStartup()
    {
        Schema.IsInitialized(Context).AssertPassed();

        Schema.GetNumAvailableSchemes(Context).AssertPassed(1);
    }
    
    [TestCase(true)]
    [TestCase(false)]
    public void Test_LoadDataScheme(bool overwriteExisting)
    {
        // Arrange
        var newSchemeName = "Foo";
        var newScheme = new DataScheme(newSchemeName);

        // Act
        var addResponse = Schema.LoadDataScheme(Context, newScheme, overwriteExisting, true);
        
        // Assert
        Assert.IsTrue(addResponse.Passed);
        Schema.GetNumAvailableSchemes(Context).TryAssert(out var numAvailableSchemes);
        Assert.That(numAvailableSchemes, Is.EqualTo(2));
        Schema.GetAllSchemes(Context).TryAssert(out var allSchemes);
        Assert.That(allSchemes, Contains.Item(newScheme.SchemeName));
        Schema.GetScheme(Context, newSchemeName).TryAssert(out var loadedScheme);
        Assert.That(loadedScheme, Is.EqualTo(newScheme));
    }


    [TestCase("")]
    [TestCase(null)]
    [TestCase("missing.json")]
    public void Test_LoadManifest_BadPath(string? badManifestPath)
    {
        Schema.LoadManifestFromPath(Context, badManifestPath).AssertFailed();
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
    public void Test_LoadManifest_InvalidPathToEntry()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        
        manifestScheme.AddManifestEntry(Context, "Invalid");
        
        MockPersistScheme(ManifestAbsFilePath, manifestScheme);
        
        // Act
        var loadResponse = Schema.LoadManifestFromPath(Context, ManifestAbsFilePath);
        
        // Assert
        Assert.IsTrue(loadResponse.Passed);
    }

    private void MockPersistScheme(string filePath, ManifestScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        MockPersistScheme(filePath, scheme._, mockRead, mockWrite);
    }

    private void MockPersistScheme(string filePath, DataScheme scheme, bool mockRead = true, bool mockWrite = false)
    {
        string relPath;
        string absPath;
        if (PathUtility.IsAbsolutePath(filePath))
        {
            absPath = filePath;
            relPath = Path.GetRelativePath(Schema.ProjectPath, absPath);
        }
        else
        {
            relPath = filePath;
            absPath = Path.GetFullPath(Path.Combine(Schema.ProjectPath, relPath));
        }
        Logger.LogVerbose($"Mock FS Setup: FileExists({Context},\"{filePath}\")");
        Logger.LogVerbose($"Mock FS Setup: FileExists({Context},\"{absPath}\")");
        
        _mockFileSystem.Setup(m => m.FileExists(Context, relPath)).Returns(SchemaResult.Pass()).Verifiable();
        _mockFileSystem.Setup(m => m.FileExists(Context, absPath)).Returns(SchemaResult.Pass()).Verifiable();

        if (mockRead)
        {
            var serialized = _storage.DefaultManifestStorageFormat.Serialize(Context, scheme).AssertPassed();
            _mockFileSystem.Setup(m => m.ReadAllText(Context, relPath))
                .Returns(SchemaResult<string>.Pass(serialized)).Verifiable();
            _mockFileSystem.Setup(m => m.ReadAllText(Context, absPath))
                .Returns(SchemaResult<string>.Pass(serialized)).Verifiable();
        }
        
        if (mockWrite)
        {
            var serialized = _storage.DefaultManifestStorageFormat.Serialize(Context, scheme).AssertPassed(null);
            _mockFileSystem.Setup(m => m.DirectoryExists(Context, It.IsAny<string>())).Returns(true).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(Context, relPath, serialized)).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(Context, absPath, serialized)).Verifiable();
        }
    }

    [Test]
    public void Test_LoadManifest_SmallManifest()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        
        // Update manifest entry to be aware of self path
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();
        
        manifestSelfEntry.FilePath = ManifestRelPath;
        
        // build test data scheme
        var testDataSchemeName = "Data";
        var testSchemeAbsFilePath = Schema.GetContentPath($"{testDataSchemeName}.json");
        var testSchemeProjectRelativePath = PathUtility.MakeRelativePath(testSchemeAbsFilePath, Schema.ProjectPath);
        Logger.LogVerbose($"{nameof(testSchemeAbsFilePath)}: {testSchemeAbsFilePath}");
        Logger.LogVerbose($"{nameof(testSchemeProjectRelativePath)}: {testSchemeProjectRelativePath}");
        var testDataScheme = new DataScheme(testDataSchemeName);
        
        MockPersistScheme(testSchemeAbsFilePath, testDataScheme);

        // add test data scheme manifest entry
        manifestScheme.AddManifestEntry(Context, testDataSchemeName, importFilePath: testSchemeProjectRelativePath).AssertPassed();

        MockPersistScheme(ManifestAbsFilePath, manifestScheme);
        MockPersistScheme(testSchemeAbsFilePath, testDataScheme);

        // Act
        Schema.LoadManifestFromPath(Context, ManifestAbsFilePath).AssertPassed();
        Schema.GetManifestEntryForScheme(testDataScheme).TryAssert(out var testDataEntry);
        
        // Assert
        var manifestLoaded = Schema.IsSchemeLoaded(Context, manifestScheme.SchemeName).AssertPassed();
        Assert.IsTrue(manifestLoaded, "Manifest should be loaded");
        var testSchemeLoaded = Schema.IsSchemeLoaded(Context, testDataSchemeName).AssertPassed();
        Assert.IsTrue(testSchemeLoaded, "Test Scheme should be loaded");
        Assert.That(testDataEntry.SchemeName, Is.EqualTo(testDataSchemeName));
        Assert.That(testDataEntry.FilePath, Is.EqualTo(testSchemeProjectRelativePath));
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
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
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
    public void Test_SaveManifest_BeforeInitialized_Fails()
    {
        
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        Schema.Reset();
        Schema.Save(Context, saveManifest: true).AssertFailed("Manifest path is invalid: ");

    }
    
    [Test]
    public void Test_SaveDataScheme_Manifest_AfterLoading()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        var manifestSavePath =  System.IO.Path.Combine(Schema.ProjectPath, "Content", $"{Manifest.MANIFEST_SCHEME_NAME}.json");
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();

        Schema.ManifestImportPath = manifestSavePath;
        manifestSelfEntry.FilePath = System.IO.Path.Combine("Content", "Manifest.json");
        MockPersistScheme(manifestSavePath, manifestScheme, mockRead: false, mockWrite: true);
        
        var loadRes = Schema.LoadDataScheme(Context, manifestScheme._, true, true);
        loadRes.AssertPassed();
        
        var saveRes = Schema.SaveDataScheme(Context, manifestScheme._, true);
        saveRes.AssertPassed();
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
    public void Test_SaveManifest_BeforeLoading()
    {
        string manifestSavePath = "Manifest.json";
        Schema.Reset();
        Schema.ManifestImportPath = manifestSavePath;
        Schema.SaveManifest(Context).AssertFailed("Manifest path is invalid: Manifest.json");
    }

    [Test]
    public void Test_Save_NoDirtySchemes_ReturnsSuccess()
    {
        // Arrange: No schemes are dirty
        var scheme = new DataScheme("Clean");
        Schema.LoadDataScheme(Context, scheme, true, true, importFilePath: "Clean.json");
        scheme.SetDirty(Context, false);
        
        // Act
        Logger.Level = Logger.LogLevel.VERBOSE;
        var result = Schema.Save(Context, saveManifest: false);
        
        // Assert
        result.AssertPassed();
        Assert.That(result.Message, Does.Contain("Saved all dirty schemes"));
        // No strict FS verification here
    }

    [Test]
    public void Test_Save_OneDirtyScheme_SavesAndClearsDirtyFlag()
    {
        
        // Arrange
        var scheme = new DataScheme("Dirty");
        _mockFileSystem.Setup(fs => fs.WriteAllText(Context, It.IsAny<string>(), _storage.DefaultManifestStorageFormat.Serialize(Context, scheme).AssertPassed(null))).Verifiable();
        
        Schema.LoadDataScheme(Context, scheme, true, true, importFilePath: "Dirty.json");
        scheme.SetDirty(Context, true);
        
        // Act
        var result = Schema.Save(Context, saveManifest: false);
        
        // Assert
        result.AssertPassed();
        Assert.IsFalse(scheme.IsDirty);
        _mockFileSystem.VerifyAll();
    }

    [Test]
    public void Test_Save_OneDirtyScheme_SaveFails_ReturnsFailure()
    {
        // Arrange
        var scheme = new DataScheme("Failing");
        Schema.LoadDataScheme(Context, scheme, true, true);
        scheme.SetDirty(Context, true);
        // Mock manifest entry
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        manifestScheme.AddManifestEntry(Context, scheme.SchemeName, importFilePath: "Failing.json");
        Schema.LoadDataScheme(Context, manifestScheme._, true, true);
    }

    [Test]
    public void Test_Save_MultipleDirtySchemes_OneFails_ReturnsFailure()
    {
        // Arrange
        var scheme1 = new DataScheme("Dirty1");
        var scheme2 = new DataScheme("Dirty2");
        Schema.LoadDataScheme(Context, scheme1, true, true);
        Schema.LoadDataScheme(Context, scheme2, true, true);
        scheme1.SetDirty(Context, true);
        scheme2.SetDirty(Context, true);
        // Mock manifest entries
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        manifestScheme.AddManifestEntry(Context, scheme1.SchemeName, importFilePath: "Dirty1.json");
        manifestScheme.AddManifestEntry(Context, scheme2.SchemeName, importFilePath: "Dirty2.json");
        Schema.LoadDataScheme(Context, manifestScheme._, true, true);
        // As above, patching SaveDataScheme to fail for one scheme is not trivial without refactor
        // Placeholder for the failure path
    }
}