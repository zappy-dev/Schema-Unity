using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Schema.Core.Logging;

namespace Schema.Core.Publish
{
    /// <summary>
    /// Turns the types a project declares into the publish steps a publish runs. Kept apart from how the types are
    /// found (the Unity editor hands over <c>TypeCache</c>'s answer) so the rules below can be tested on their own.
    /// </summary>
    public static class SchemaPublishSteps
    {
        /// <summary>
        /// Builds one instance of each usable type. A type that cannot be built is skipped with a warning naming it,
        /// never an exception: one broken step in a project must not stop the Schema editor from publishing.
        /// Skipped: abstract types and interfaces, open generic types, and types with no public parameterless
        /// constructor. The result is ordered by type name, so a publish run is the same twice over.
        /// </summary>
        public static IReadOnlyList<ISchemaPublishStep> Create(IEnumerable<Type> candidateTypes)
        {
            var steps = new List<ISchemaPublishStep>();
            if (candidateTypes == null) return steps;

            foreach (var type in candidateTypes.Where(t => t != null).OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (!typeof(ISchemaPublishStep).IsAssignableFrom(type))
                {
                    Logger.LogWarning($"Skipping publish step '{type.FullName}': it does not implement {nameof(ISchemaPublishStep)}.");
                    continue;
                }

                if (type.IsAbstract || type.IsInterface) continue;

                if (type.IsGenericTypeDefinition)
                {
                    Logger.LogWarning($"Skipping publish step '{type.FullName}': a generic type cannot be built by Schema. Use a concrete type.");
                    continue;
                }

                if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) == null)
                {
                    Logger.LogWarning($"Skipping publish step '{type.FullName}': it needs a public parameterless constructor.");
                    continue;
                }

                try
                {
                    steps.Add((ISchemaPublishStep)Activator.CreateInstance(type));
                }
                catch (Exception e)
                {
                    var reason = (e is TargetInvocationException invocation && invocation.InnerException != null)
                        ? invocation.InnerException.Message
                        : e.Message;
                    Logger.LogWarning($"Skipping publish step '{type.FullName}': it could not be constructed: {reason}");
                }
            }

            return steps;
        }
    }
}
