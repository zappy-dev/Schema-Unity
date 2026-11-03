using System.Linq;
using NUnit.Framework;
using Schema.Core;
using Schema.Core.Publish;

namespace Schema.Unity.Editor.Tests
{
    /// <summary>
    /// A project adds a publish step by writing one class: nothing to register, nothing to tick. This fixture's own
    /// step stands in for the project's (the tank game's tuning baker) and has to be found in the Unity project.
    /// </summary>
    public class SchemaPublishStepRegistryTests
    {
        /// <summary>Names a scheme no project has, so finding it here never changes what a real publish writes.</summary>
        public sealed class TestOnlyPublishStep : SchemaPublishStep
        {
            public const string OnlyScheme = "Schema.Tests.NoSuchScheme";

            public override int Order => 1234;
            protected override string SchemeName => OnlyScheme;
            protected override SchemaResult PublishScheme(SchemaPublishRequest request) => SchemaResult.Pass();
        }

        [Test]
        public void Steps_FindAStepDeclaredInTheProject()
        {
            SchemaPublishStepRegistry.Invalidate();

            var steps = SchemaPublishStepRegistry.Steps;

            Assert.That(steps.Select(s => s.GetType()), Does.Contain(typeof(TestOnlyPublishStep)),
                "a type implementing ISchemaPublishStep anywhere in the project should be found and run by Publish");
            Assert.That(steps.Single(s => s is TestOnlyPublishStep).Order, Is.EqualTo(1234));
        }

        [Test]
        public void Steps_AreCachedUntilInvalidated()
        {
            SchemaPublishStepRegistry.Invalidate();
            var first = SchemaPublishStepRegistry.Steps;

            Assert.That(SchemaPublishStepRegistry.Steps, Is.SameAs(first));

            SchemaPublishStepRegistry.Invalidate();
            Assert.That(SchemaPublishStepRegistry.Steps, Is.Not.SameAs(first));
        }
    }
}
