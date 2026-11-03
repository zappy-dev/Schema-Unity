using Schema.Core.Publish;

namespace Schema.Core.Tests.Publish;

/// <summary>
/// Turning the types a project declares into the publish steps a publish runs. One broken step in a project must
/// not stop the others, or the editor, from publishing.
/// </summary>
[TestFixture]
public class TestSchemaPublishSteps
{
    public class GoodStep : ISchemaPublishStep
    {
        public int Order => 0;
        public SchemaResult Publish(SchemaPublishRequest request) => SchemaResult.Pass();
    }

    public class AlsoGoodStep : ISchemaPublishStep
    {
        public int Order => 1;
        public SchemaResult Publish(SchemaPublishRequest request) => SchemaResult.Pass();
    }

    public abstract class AbstractStep : ISchemaPublishStep
    {
        public int Order => 0;
        public abstract SchemaResult Publish(SchemaPublishRequest request);
    }

    public class GenericStep<T> : ISchemaPublishStep
    {
        public int Order => 0;
        public SchemaResult Publish(SchemaPublishRequest request) => SchemaResult.Pass();
    }

    public class NeedsArgumentsStep : ISchemaPublishStep
    {
        public NeedsArgumentsStep(string why) { }
        public int Order => 0;
        public SchemaResult Publish(SchemaPublishRequest request) => SchemaResult.Pass();
    }

    public class ThrowsInConstructorStep : ISchemaPublishStep
    {
        public ThrowsInConstructorStep() => throw new InvalidOperationException("no baker here");
        public int Order => 0;
        public SchemaResult Publish(SchemaPublishRequest request) => SchemaResult.Pass();
    }

    public class NotAStep
    {
    }

    [Test]
    public void Create_BuildsOneInstanceOfEachUsableType()
    {
        var steps = SchemaPublishSteps.Create(new[] { typeof(GoodStep), typeof(AlsoGoodStep) });

        Assert.That(steps.Select(s => s.GetType()), Is.EquivalentTo(new[] { typeof(GoodStep), typeof(AlsoGoodStep) }));
    }

    [Test]
    public void Create_IsDeterministic_WhateverOrderTheTypesArriveIn()
    {
        var one = SchemaPublishSteps.Create(new[] { typeof(GoodStep), typeof(AlsoGoodStep) }).Select(s => s.GetType().FullName);
        var other = SchemaPublishSteps.Create(new[] { typeof(AlsoGoodStep), typeof(GoodStep) }).Select(s => s.GetType().FullName);

        Assert.That(one, Is.EqualTo(other));
    }

    [Test]
    public void Create_SkipsTypesItCannotBuild_AndKeepsTheRest()
    {
        var steps = SchemaPublishSteps.Create(new[]
        {
            typeof(AbstractStep),
            typeof(GenericStep<>),
            typeof(NeedsArgumentsStep),
            typeof(ThrowsInConstructorStep),
            typeof(NotAStep),
            typeof(ISchemaPublishStep),
            null,
            typeof(GoodStep),
        });

        Assert.That(steps.Select(s => s.GetType()), Is.EqualTo(new[] { typeof(GoodStep) }));
    }

    [Test]
    public void Create_OfNothing_IsNoSteps()
    {
        Assert.That(SchemaPublishSteps.Create(null), Is.Empty);
        Assert.That(SchemaPublishSteps.Create(Array.Empty<Type>()), Is.Empty);
    }
}
