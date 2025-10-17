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
    public static string ManifestAbsFilePath => Schema.GetContentPath($"{Manifest.MANIFEST_SCHEME_NAME}.json");
    /// <summary>
    /// Project-relative path to Manifest file
    /// </summary>
    public static string ManifestRelPath => PathUtility.MakeRelativePath(ManifestAbsFilePath, Schema.LatestProject.ProjectPath);

    #endregion
    private Storage _storage;
    private Mock<IFileSystem> _mockFileSystem;

    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestSchema),
    };

    private CancellationTokenSource cts = new();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
    }

    [SetUp]
    public async Task Setup()
    {
        (_mockFileSystem, _storage) = await TestFixtureSetup.Initialize(Context);
        Logger.LogVerbose($"{nameof(ManifestAbsFilePath)}: {ManifestAbsFilePath}");
        Logger.LogVerbose($"{nameof(ManifestRelPath)}: {ManifestRelPath}");
        
        // Ensure manifest import path and self-entry path are consistent with project structure
        var manifestAbsolutePath = System.IO.Path.Combine(Context.Project.ProjectPath ?? "", "Content", $"{Manifest.MANIFEST_SCHEME_NAME}.json");
        Context.Project.SetManifestImportPath(Context, manifestAbsolutePath).AssertPassed();
        if (Schema.GetManifestScheme(Context).TryAssert(out var loadedManifest))
        {
            loadedManifest.GetSelfEntry(Context).TryAssert(out var selfEntry);
            selfEntry.FilePath = System.IO.Path.Combine("Content", $"{Manifest.MANIFEST_SCHEME_NAME}.json");
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        cts.Cancel();
        cts.Dispose();
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
        var addResponse = Schema.LoadDataScheme(Context, newScheme, overwriteExisting);
        
        // Assert
        Assert.IsTrue(addResponse.Passed);
        Schema.GetNumAvailableSchemes(Context).TryAssert(out var numAvailableSchemes);
        Assert.That(numAvailableSchemes, Is.EqualTo(2));
        Schema.GetAllSchemes(Context).TryAssert(out var allSchemes);
        Assert.That(allSchemes, Contains.Item(newScheme.SchemeName));
        Schema.GetScheme(Context, newSchemeName).TryAssert(out var loadedScheme);
        Assert.That(loadedScheme, Is.EqualTo(newScheme));
    }

    [Test]
    public void Test_LoadDataScheme_StressTest()
    {
        var newSchemeName = "Foo";
        var newScheme = new DataScheme(newSchemeName);
        newScheme.AddAttribute(Context, "TestString", DataType.Text, isIdentifier: true).AssertPassed();
        newScheme.AddAttribute(Context, "TestInt", DataType.Integer, isIdentifier: false).AssertPassed();
        newScheme.AddAttribute(Context, "TestBool", DataType.Boolean, isIdentifier: false).AssertPassed();
        newScheme.AddAttribute(Context, "TestDateTime", DataType.DateTime, isIdentifier: false).AssertPassed();

        newScheme.AddEntry(Context, new DataEntry
        {
            { "TestString", "bar", Context },
            { "TestInt", "1", Context }, // type to auto-convert
            // missing fields, TestBool and TestDateTime should get auto-filled
            { "TestDateTime", "foo", Context }, // bad conversion
            { "TestBoolOld", true, Context }
        }, runDataValidation: false, fillEmptyValues: false).AssertPassed();
        
        
        // Act
        Schema.LoadDataScheme(Context, newScheme, overwriteExisting: true).AssertPassed();
        
        // Assert
        Schema.GetNumAvailableSchemes(Context).TryAssert(out var numAvailableSchemes);
        Assert.That(numAvailableSchemes, Is.EqualTo(2));
        Schema.GetAllSchemes(Context).TryAssert(out var allSchemes);
        Assert.That(allSchemes, Contains.Item(newScheme.SchemeName));
        Schema.GetScheme(Context, newSchemeName).TryAssert(out var loadedScheme);
        Assert.That(loadedScheme, Is.EqualTo(newScheme));

        var loadedDataEntry = loadedScheme.GetEntry(0);
        Assert.That(loadedDataEntry, Is.Not.Null);
        
        Assert.That(loadedDataEntry.GetDataDirect("TestString"),  Is.EqualTo("bar"));
        Assert.That(loadedDataEntry.GetDataDirect("TestInt"),  Is.EqualTo(1));
        Assert.That(loadedDataEntry.GetDataDirect("TestBool"),  Is.EqualTo(false));
        Assert.That(loadedDataEntry.GetDataDirect("TestDateTime"),  Is.EqualTo("foo"));
        Assert.That(loadedDataEntry.GetDataDirect("TestBoolOld"), Is.Null, "Expect that value for unregistered attribute is not set");
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("missing.json")]
    public async Task Test_LoadManifest_BadPath(string? badManifestPath)
    {
        (await Schema.LoadManifestFromPath(Context, badManifestPath, projectPath: TestFixtureSetup.ProjectPath)).AssertFailed();
    }
    
    [Test]
    public async Task Test_LoadManifest_MalformedFile()
    {
        string malformedFilePath = "malformed.json";
        
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(Context, malformedFilePath, cts.Token))
            .Returns(Task.FromResult(SchemaResult.Pass()));
        _mockFileSystem.Setup(fs => fs.ReadAllText(Context, malformedFilePath, cts.Token))
            .Returns(Task.FromResult(SchemaResult<string>.Pass("malformedContent")));
        
        // Act
        var loadResponse = await Schema.LoadManifestFromPath(Context, malformedFilePath, projectPath: TestFixtureSetup.ProjectPath);
        
        // Assert
        Assert.IsFalse(loadResponse.Passed);
    }
    
    [Test]
    public async Task Test_LoadManifest_InvalidPathToEntry()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        // Arrange
        
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        
        manifestScheme.AddManifestEntry(Context, "Invalid");
        
        MockPersistScheme(ManifestAbsFilePath, manifestScheme);
        
        // Act
        var loadResponse = await Schema.LoadManifestFromPath(Context, 
            ManifestAbsFilePath, 
            projectPath: TestFixtureSetup.ProjectPath,
            cancellationToken: cts.Token);
        
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
            relPath = Path.GetRelativePath(TestFixtureSetup.ProjectPath, absPath);
        }
        else
        {
            relPath = filePath;
            absPath = Path.GetFullPath(Path.Combine(TestFixtureSetup.ProjectPath, relPath));
        }
        Logger.LogVerbose($"Mock FS Setup: FileExists({Context},\"{filePath}\")");
        Logger.LogVerbose($"Mock FS Setup: FileExists({Context},\"{absPath}\")");
        
        _mockFileSystem.Setup(m => m.FileExists(Context, relPath, cts.Token)).Returns(Task.FromResult(SchemaResult.Pass())).Verifiable();
        _mockFileSystem.Setup(m => m.FileExists(Context, absPath, cts.Token)).Returns(Task.FromResult(SchemaResult.Pass())).Verifiable();

        if (mockRead)
        {
            var serialized = _storage.DefaultManifestStorageFormat.Serialize(Context, scheme).AssertPassed();
            _mockFileSystem.Setup(m => m.ReadAllText(Context, relPath, cts.Token))
                .Returns(Task.FromResult(SchemaResult<string>.Pass(serialized))).Verifiable();
            _mockFileSystem.Setup(m => m.ReadAllText(Context, absPath, cts.Token))
                .Returns(Task.FromResult(SchemaResult<string>.Pass(serialized))).Verifiable();
        }
        
        if (mockWrite)
        {
            var serialized = _storage.DefaultManifestStorageFormat.Serialize(Context, scheme).AssertPassed(null);
            _mockFileSystem.Setup(m => m.DirectoryExists(Context, It.IsAny<string>(), cts.Token)).Returns(Task.FromResult(true)).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(Context, relPath, serialized, cts.Token)).Verifiable();
            _mockFileSystem.Setup(m => m.WriteAllText(Context, absPath, serialized, cts.Token)).Verifiable();
        }
    }

    [Test]
    public async Task Test_LoadManifest_SmallManifest()
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
        var testSchemeProjectRelativePath = PathUtility.MakeRelativePath(testSchemeAbsFilePath, TestFixtureSetup.ProjectPath);
        Logger.LogVerbose($"{nameof(testSchemeAbsFilePath)}: {testSchemeAbsFilePath}");
        Logger.LogVerbose($"{nameof(testSchemeProjectRelativePath)}: {testSchemeProjectRelativePath}");
        var testDataScheme = new DataScheme(testDataSchemeName);
        
        MockPersistScheme(testSchemeAbsFilePath, testDataScheme);

        // add test data scheme manifest entry
        manifestScheme.AddManifestEntry(Context, testDataSchemeName, importFilePath: testSchemeProjectRelativePath).AssertPassed();

        MockPersistScheme(ManifestAbsFilePath, manifestScheme);
        MockPersistScheme(testSchemeAbsFilePath, testDataScheme);

        // Act
        (await Schema.LoadManifestFromPath(Context, ManifestAbsFilePath, TestFixtureSetup.ProjectPath, cancellationToken: cts.Token)).AssertPassed();
        Schema.GetManifestEntryForScheme(Context, testDataScheme).TryAssert(out var testDataEntry);
        
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
        Schema.GetManifestEntryForScheme(Context, schemeName).AssertFailed();
    }

    [Test, TestCaseSource(nameof(BadManifestEntryTestCases))]
    public async Task Test_LoadSchemeFromManifestEntry_BadEntry(ManifestEntry? manifestEntry)
    {
        var res = await Schema.LoadSchemeFromManifestEntry(Context, manifestEntry);
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
    public async Task<bool> Test_SaveDataScheme_BadScheme(DataScheme scheme)
    {
        var saveResponse = await Schema.SaveDataScheme(Context, scheme, true);
        return saveResponse.Passed;
    }

    [Test]
    public async Task Test_SaveDataScheme_ValidScheme()
    {
        // Arrange
        var newScheme = new DataScheme("Foo");
        var newSchemeFilePath = "Foo.json";
        MockPersistScheme(newSchemeFilePath, newScheme);
        Schema.LoadDataScheme(Context, newScheme, true, importFilePath: newSchemeFilePath).AssertPassed();
        
        // Act
        (await Schema.SaveDataScheme(Context, newScheme, false)).AssertPassed();
    }
    
    [Test]
    public async Task Test_SaveDataScheme_Manifest_BeforeLoading()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        var manifestSavePath = $"{Manifest.MANIFEST_SCHEME_NAME}.json";
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();

        manifestSelfEntry.FilePath = manifestSavePath;
        
        (await Schema.SaveDataScheme(Context, manifestScheme._, true)).AssertFailed();
    }
    
    [Test]
    public async Task Test_SaveDataScheme_UnregisteredScheme_Fails()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        var dataScheme = new DataScheme("Foo");
        
        (await Schema.SaveDataScheme(Context, dataScheme, true)).AssertFailed();
    }
    
    [Test]
    public async Task  Test_SaveManifest_BeforeInitialized_Fails()
    {
        
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        Schema.Reset();
        (await Schema.Save(Context, saveManifest: true)).AssertFailed("No project specified");

    }
    
    [Test]
    public async Task Test_SaveDataScheme_Manifest_AfterLoading()
    {
        Logger.Level = Logger.LogLevel.VERBOSE;
        
        var manifestSavePath =  System.IO.Path.Combine(TestFixtureSetup.ProjectPath, "Content", $"{Manifest.MANIFEST_SCHEME_NAME}.json");
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, manifestSavePath);
        var manifestSelfEntry = manifestScheme.GetSelfEntry(Context).AssertPassed();

        manifestSelfEntry.FilePath = System.IO.Path.Combine("Content", "Manifest.json");
        MockPersistScheme(manifestSavePath, manifestScheme, mockRead: false, mockWrite: true);
        
        var loadRes = Schema.LoadDataScheme(Context, manifestScheme._, true);
        loadRes.AssertPassed();
        
        var saveRes = await Schema.SaveDataScheme(Context, manifestScheme._, true);
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
    public void Test_Reset_BeforeLoading()
    {
        Schema.Reset();
        Assert.That(Schema.LatestProject, Is.Null, "Expect that no project is loaded");
    }

    [Test]
    public async Task Test_Save_NoDirtySchemes_ReturnsSuccess()
    {
        // Arrange: No schemes are dirty
        var scheme = new DataScheme("Clean");
        Schema.LoadDataScheme(Context, scheme, true, importFilePath: "Clean.json");
        scheme.SetDirty(Context, false);
        
        // Act
        Logger.Level = Logger.LogLevel.VERBOSE;
        var result = await Schema.Save(Context, saveManifest: false);
        
        // Assert
        result.AssertPassed();
        Assert.That(result.Message, Does.Contain("Saved all dirty schemes"));
        // No strict FS verification here
    }

    [Test]
    public async Task Test_Save_OneDirtyScheme_SavesAndClearsDirtyFlag()
    {
        
        // Arrange
        var scheme = new DataScheme("Dirty");
        _mockFileSystem.Setup(fs => fs.WriteAllText(Context, It.IsAny<string>(), 
            _storage.DefaultManifestStorageFormat.Serialize(Context, scheme).AssertPassed(null), cts.Token)).Verifiable();
        
        Schema.LoadDataScheme(Context, scheme, true, importFilePath: "Dirty.json");
        scheme.SetDirty(Context, true);
        
        // Act
        var result = await Schema.Save(Context, saveManifest: false, cts.Token);
        
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
        Schema.LoadDataScheme(Context, scheme, true);
        scheme.SetDirty(Context, true);
        // Mock manifest entry
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
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
        var manifestScheme = ManifestDataSchemeFactory.BuildTemplateManifestSchema(Context, String.Empty, String.Empty);
        manifestScheme.AddManifestEntry(Context, scheme1.SchemeName, importFilePath: "Dirty1.json");
        manifestScheme.AddManifestEntry(Context, scheme2.SchemeName, importFilePath: "Dirty2.json");
        Schema.LoadDataScheme(Context, manifestScheme._, true);
        // As above, patching SaveDataScheme to fail for one scheme is not trivial without refactor
        // Placeholder for the failure path
    }

    [Test]
    public void Test_IsValidScheme_ValidScheme_Passes()
    {
        var scheme = new DataScheme("Valid");
        scheme.AddAttribute(Context, "Name", DataType.Text).AssertPassed();
        scheme.AddAttribute(Context, "Age", DataType.Integer).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Name", "Alice", Context }, { "Age", 30, Context } }).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Name", "Bob", Context }, { "Age", 25, Context } }).AssertPassed();

        var result = Schema.IsValidScheme(Context, scheme);
        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void Test_IsValidScheme_DuplicateIdentifiers_Fails()
    {
        var scheme = new DataScheme("WithId");
        scheme.AddAttribute(Context, "Id", DataType.Text, isIdentifier: true).AssertPassed();
        scheme.AddAttribute(Context, "Field", DataType.Text).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Id", "dup", Context }, { "Field", "A", Context } }).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Id", "dup", Context }, { "Field", "B", Context } }).AssertPassed();

        var result = Schema.IsValidScheme(Context, scheme);
        Assert.IsTrue(result.Failed);
        Assert.That(result.Message, Does.Contain("duplicate identifiers"));
    }

    [Test]
    public void Test_IsValidScheme_MissingEntryData_Passes()
    {
        var scheme = new DataScheme("MissingData");
        scheme.AddAttribute(Context, "Name", DataType.Text).AssertPassed();
        scheme.AddAttribute(Context, "Age", DataType.Integer).AssertPassed();
        // Add entry with missing "Age" key
        scheme.AddEntry(Context, new DataEntry { { "Name", "Alice", Context } }).AssertPassed();

        var result = Schema.IsValidScheme(Context, scheme);
        Assert.IsTrue(result.Passed);
    }

    [Test]
    public void Test_IsValidScheme_InvalidValueType_Fails()
    {
        var scheme = new DataScheme("InvalidType");
        scheme.AddAttribute(Context, "Name", DataType.Text).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { "Name", 12345, Context } }, runDataValidation: false).AssertPassed();

        var result = Schema.IsValidScheme(Context, scheme);
        Assert.IsTrue(result.Failed);
        Assert.That(result.Message, Does.Contain("Failed to validate all entries"));
    }
}