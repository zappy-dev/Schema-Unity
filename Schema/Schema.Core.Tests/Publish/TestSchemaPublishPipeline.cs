using Moq;
using Schema.Core.IO;
using Schema.Core.Publish;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Publish;

/// <summary>
/// What one scheme's Publish does, and in what order. A project can add its own publish step, and switch the
/// built-in outputs off, so that Publish does only what the project does (the tank game bakes its tuning table into
/// a ScriptableObject of its own instead of a Resources JSON).
/// </summary>
[TestFixture]
public class TestSchemaPublishPipeline
{
    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestSchemaPublishPipeline),
    };

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        Schema.SetStorage(new Storage(new Mock<IFileSystem>().Object));
        Schema.InitializeTemplateManifestScheme(Context);
    }

    [TearDown]
    public void TearDown() => Schema.Reset();

    private static SchemaPublishRequest Request(string schemeName = "MachineGuns") =>
        new(Context, ScratchSchemes.Table(schemeName, "Coax", "Pintle"));

    /// <summary>A step that records that it ran, in the order it ran.</summary>
    private sealed class RecordingStep : ISchemaPublishStep
    {
        private readonly List<string> log;
        private readonly string name;
        private readonly SchemaResult result;

        public RecordingStep(List<string> log, string name, int order = 0, SchemaResult? result = null)
        {
            this.log = log;
            this.name = name;
            Order = order;
            this.result = result ?? SchemaResult.Pass();
        }

        public int Order { get; }

        public SchemaResult Publish(SchemaPublishRequest request)
        {
            log.Add(name);
            return result;
        }
    }

    private sealed class ThrowingStep : ISchemaPublishStep
    {
        public int Order => 0;
        public SchemaResult Publish(SchemaPublishRequest request) => throw new InvalidOperationException("the baker blew up");
    }

    [Test]
    public void Run_WritesBothBuiltInOutputs_ByDefault()
    {
        var log = new List<string>();

        var res = SchemaPublishPipeline.Run(Request(), SchemaPublishSettings.Defaults(), null,
            _ => { log.Add("resources"); return SchemaResult.Pass(); },
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertPassed();
        Assert.That(log, Is.EqualTo(new[] { "resources", "csharp" }));
    }

    [Test]
    public void Run_SkipsTheResourcesJson_WhenTheProjectSwitchedItOff()
    {
        var log = new List<string>();
        var settings = new SchemaPublishSettings { PublishResourcesJson = false };

        var res = SchemaPublishPipeline.Run(Request(), settings, null,
            _ => { log.Add("resources"); return SchemaResult.Pass(); },
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertPassed();
        Assert.That(log, Is.EqualTo(new[] { "csharp" }));
    }

    [Test]
    public void Run_SkipsTheGeneratedCode_WhenTheProjectSwitchedItOff()
    {
        var log = new List<string>();
        var settings = new SchemaPublishSettings { GenerateCSharp = false };

        var res = SchemaPublishPipeline.Run(Request(), settings, null,
            _ => { log.Add("resources"); return SchemaResult.Pass(); },
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertPassed();
        Assert.That(log, Is.EqualTo(new[] { "resources" }));
    }

    /// <summary>The tank game's case: no built-in output at all, only the project's own baker.</summary>
    [Test]
    public void Run_RunsOnlyTheProjectStep_WhenBothBuiltInOutputsAreOff()
    {
        var log = new List<string>();
        var settings = new SchemaPublishSettings { PublishResourcesJson = false, GenerateCSharp = false };
        var steps = new ISchemaPublishStep[] { new RecordingStep(log, "baker") };

        var res = SchemaPublishPipeline.Run(Request(), settings, steps,
            _ => { log.Add("resources"); return SchemaResult.Pass(); },
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertPassed();
        Assert.That(log, Is.EqualTo(new[] { "baker" }));
    }

    [Test]
    public void Run_RunsProjectSteps_AfterTheBuiltInOutputs_InOrder()
    {
        var log = new List<string>();
        var steps = new ISchemaPublishStep[]
        {
            new RecordingStep(log, "late", order: 10),
            new RecordingStep(log, "early", order: -5),
            new RecordingStep(log, "middle"),
        };

        var res = SchemaPublishPipeline.Run(Request(), SchemaPublishSettings.Defaults(), steps,
            _ => { log.Add("resources"); return SchemaResult.Pass(); },
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertPassed();
        Assert.That(log, Is.EqualTo(new[] { "resources", "csharp", "early", "middle", "late" }));
        Assert.That(res.Message, Does.Contain("3 project publish steps"), res.Message);
    }

    [Test]
    public void Run_FailsNamingTheStep_WhenAStepFails()
    {
        var log = new List<string>();
        var steps = new ISchemaPublishStep[]
        {
            new RecordingStep(log, "baker", order: 0, result: SchemaResult.Fail(Context, "TankTuning.asset is read-only")),
            new RecordingStep(log, "after", order: 1),
        };

        var res = SchemaPublishPipeline.Run(Request(), SchemaPublishSettings.Defaults(), steps,
            _ => SchemaResult.Pass(), _ => SchemaResult.Pass());

        res.AssertFailed();
        Assert.That(res.Message, Does.Contain(nameof(RecordingStep)).And.Contain("MachineGuns").And.Contain("read-only"), res.Message);
        Assert.That(log, Is.EqualTo(new[] { "baker" }), "a failed step halts the publish");
    }

    [Test]
    public void Run_FailsNamingTheStep_WhenAStepThrows()
    {
        var res = SchemaPublishPipeline.Run(Request(), SchemaPublishSettings.Defaults(),
            new ISchemaPublishStep[] { new ThrowingStep() },
            _ => SchemaResult.Pass(), _ => SchemaResult.Pass());

        res.AssertFailed();
        Assert.That(res.Message, Does.Contain(nameof(ThrowingStep)).And.Contain("the baker blew up"), res.Message);
    }

    [Test]
    public void Run_StopsAtTheFirstFailedBuiltInOutput()
    {
        var log = new List<string>();
        var steps = new ISchemaPublishStep[] { new RecordingStep(log, "baker") };

        var res = SchemaPublishPipeline.Run(Request(), SchemaPublishSettings.Defaults(), steps,
            _ => SchemaResult.Fail(Context, "could not write the Resources JSON"),
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertFailed();
        Assert.That(res.Message, Does.Contain("could not write the Resources JSON"));
        Assert.That(log, Is.Empty, "nothing runs after a failed output");
    }

    [Test]
    public void Run_SaysItDidNothing_WhenEverythingIsOffAndThereAreNoSteps()
    {
        var settings = new SchemaPublishSettings { PublishResourcesJson = false, GenerateCSharp = false };

        var res = SchemaPublishPipeline.Run(Request(), settings, Array.Empty<ISchemaPublishStep>(),
            _ => SchemaResult.Pass(), _ => SchemaResult.Pass());

        res.AssertPassed();
        Assert.That(res.Message, Does.Contain("did nothing").And.Contain("MachineGuns"), res.Message);
    }

    [Test]
    public void Run_TreatsNullSettings_AsTheDefaults()
    {
        var log = new List<string>();

        var res = SchemaPublishPipeline.Run(Request(), null, null,
            _ => { log.Add("resources"); return SchemaResult.Pass(); },
            _ => { log.Add("csharp"); return SchemaResult.Pass(); });

        res.AssertPassed();
        Assert.That(log, Is.EqualTo(new[] { "resources", "csharp" }));
    }

    [Test]
    public void Run_FailsWithNoScheme()
    {
        var res = SchemaPublishPipeline.Run(new SchemaPublishRequest(Context, null), SchemaPublishSettings.Defaults(), null,
            _ => SchemaResult.Pass(), _ => SchemaResult.Pass());

        res.AssertFailed();
    }

    [Test]
    public void SchemaPublishStep_OnlyRunsForTheSchemeItNames()
    {
        var baker = new TuningBaker();

        baker.Publish(new SchemaPublishRequest(Context, ScratchSchemes.Table("Armor"))).AssertPassed();
        Assert.That(baker.BakedSchemes, Is.Empty, "a step that names a scheme is not run for the others");

        baker.Publish(Request()).AssertPassed();
        Assert.That(baker.BakedSchemes, Is.EqualTo(new[] { "MachineGuns" }));
    }

    /// <summary>The shape a project's step takes: derive, name the table, bake it.</summary>
    private sealed class TuningBaker : SchemaPublishStep
    {
        public List<string> BakedSchemes { get; } = new();

        protected override string SchemeName => "MachineGuns";

        protected override SchemaResult PublishScheme(SchemaPublishRequest request)
        {
            BakedSchemes.Add(request.SchemeName!);
            return SchemaResult.Pass();
        }
    }
}
