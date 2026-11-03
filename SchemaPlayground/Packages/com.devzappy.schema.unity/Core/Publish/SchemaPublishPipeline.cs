using System;
using System.Collections.Generic;
using System.Linq;

namespace Schema.Core.Publish
{
    /// <summary>
    /// What one scheme's Publish does, in order: the built-in outputs the project's
    /// <see cref="SchemaPublishSettings"/> leaves on, then the project's own <see cref="ISchemaPublishStep"/>s.
    /// The outputs are passed in as delegates because writing them needs the editor (the asset database, the
    /// manifest's export paths); the order, the switches and what a failure means live here, where they are tested.
    /// </summary>
    public static class SchemaPublishPipeline
    {
        /// <summary>
        /// Publishes one scheme. Stops at the first failure and says which part failed, so a half-published scheme
        /// is never reported as published.
        /// </summary>
        /// <param name="request">The scheme being published, and the context to publish it under.</param>
        /// <param name="settings">The project's switches. Null is read as <see cref="SchemaPublishSettings.Defaults"/>.</param>
        /// <param name="projectSteps">The project's own steps, in any order: they are run by <see cref="ISchemaPublishStep.Order"/>.</param>
        /// <param name="publishResourcesJson">Writes the scheme's data to its publish target. Skipped when the setting is off.</param>
        /// <param name="generateCSharp">Generates the scheme's C# wrappers. Skipped when the setting is off.</param>
        public static SchemaResult Run(
            SchemaPublishRequest request,
            SchemaPublishSettings settings,
            IEnumerable<ISchemaPublishStep> projectSteps,
            Func<SchemaPublishRequest, SchemaResult> publishResourcesJson,
            Func<SchemaPublishRequest, SchemaResult> generateCSharp)
        {
            var context = request.Context;
            var schemeName = request.SchemeName;
            if (request.Scheme == null)
            {
                return SchemaResult.Fail(context, "No scheme to publish");
            }

            settings = settings ?? SchemaPublishSettings.Defaults();
            var ran = new List<string>();

            if (settings.PublishResourcesJson && publishResourcesJson != null)
            {
                var dataRes = publishResourcesJson(request);
                if (dataRes.Failed) return dataRes;
                ran.Add("the Resources JSON");
            }

            if (settings.GenerateCSharp && generateCSharp != null)
            {
                var codeRes = generateCSharp(request);
                if (codeRes.Failed) return codeRes;
                ran.Add("the generated C#");
            }

            var ordered = (projectSteps ?? Enumerable.Empty<ISchemaPublishStep>())
                .Where(step => step != null)
                .OrderBy(step => step.Order)
                .ToList();

            foreach (var step in ordered)
            {
                var stepName = step.GetType().FullName;
                SchemaResult stepRes;
                try
                {
                    stepRes = step.Publish(request);
                }
                catch (Exception e)
                {
                    return SchemaResult.Fail(context, $"Publish step '{stepName}' threw publishing scheme '{schemeName}': {e.Message}");
                }

                if (stepRes.Failed)
                {
                    return SchemaResult.Fail(context, $"Publish step '{stepName}' failed publishing scheme '{schemeName}': {stepRes.Message}");
                }
            }

            if (ordered.Count > 0)
            {
                ran.Add(ordered.Count == 1 ? "1 project publish step" : $"{ordered.Count} project publish steps");
            }

            if (ran.Count == 0)
            {
                return SchemaResult.Pass(
                    $"Publish did nothing for scheme '{schemeName}': the Resources JSON and the generated C# are switched off in the project's publish settings, and the project has no publish steps.",
                    context);
            }

            return SchemaResult.Pass($"Published scheme '{schemeName}': {string.Join(", ", ran)}.", context);
        }
    }
}
