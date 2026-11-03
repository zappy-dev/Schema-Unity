using System.Collections.Generic;
using Schema.Core.Publish;
using UnityEditor;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// The project's own publish steps, as found in this Unity project. <see cref="TypeCache"/> already knows every
    /// type in the project that implements <see cref="ISchemaPublishStep"/>, so a project adds a step by writing one
    /// class: there is nothing to register and nothing to tick.
    /// </summary>
    /// <remarks>
    /// The instances are cached for the life of the domain. Unity rebuilds the domain whenever a script changes, so
    /// a newly written step is picked up on the next compile; <see cref="Invalidate"/> is there for the settings UI.
    /// </remarks>
    internal static class SchemaPublishStepRegistry
    {
        private static IReadOnlyList<ISchemaPublishStep> cached;

        public static IReadOnlyList<ISchemaPublishStep> Steps =>
            cached ?? (cached = SchemaPublishSteps.Create(TypeCache.GetTypesDerivedFrom<ISchemaPublishStep>()));

        public static void Invalidate() => cached = null;
    }
}
