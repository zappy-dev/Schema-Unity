using System;
using Schema.Core.Data;

namespace Schema.Core.Publish
{
    /// <summary>
    /// A publish step the host project writes itself. Every type in the project that implements this is found and
    /// run as part of a Publish, after the built-in outputs that <see cref="SchemaPublishSettings"/> leaves on, so a
    /// project can add to what Publish does, or (with both built-in outputs switched off) replace it entirely: a game
    /// can bake the authored table straight into its own asset instead of a Resources JSON.
    ///
    /// Implementations need a public parameterless constructor; one instance is built per publish run. A step that
    /// returns a failed <see cref="SchemaResult"/>, or throws, fails the publish and is named in the message, so a
    /// bad bake is never mistaken for a good one.
    ///
    /// <see cref="SchemaPublishStep"/> is the easier starting point: it defaults <see cref="Order"/> and can filter
    /// down to a single scheme.
    /// </summary>
    public interface ISchemaPublishStep
    {
        /// <summary>
        /// Steps run from the lowest order to the highest. Steps that share an order run in a stable, deterministic
        /// order (by type name), so a publish never changes its mind between runs.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Publishes one scheme. Called once per scheme being published, so a step that only cares about one table
        /// should check <see cref="SchemaPublishRequest.SchemeName"/> and pass straight through otherwise.
        /// </summary>
        SchemaResult Publish(SchemaPublishRequest request);
    }

    /// <summary>
    /// The usual base class for a project's publish step: gives <see cref="Order"/> a default, and lets a step name
    /// the one scheme it publishes so it does not have to check the scheme name itself.
    /// </summary>
    public abstract class SchemaPublishStep : ISchemaPublishStep
    {
        /// <inheritdoc />
        public virtual int Order => 0;

        /// <summary>
        /// The scheme this step publishes. Null or empty (the default) means every scheme: the step is then called
        /// once per published scheme.
        /// </summary>
        protected virtual string SchemeName => null;

        /// <inheritdoc />
        public SchemaResult Publish(SchemaPublishRequest request)
        {
            var wanted = SchemeName;
            if (!string.IsNullOrEmpty(wanted) && !string.Equals(wanted, request.SchemeName, StringComparison.Ordinal))
            {
                return SchemaResult.Pass($"Publish step '{GetType().Name}' skipped scheme '{request.SchemeName}': it publishes '{wanted}'.", request.Context);
            }

            return PublishScheme(request);
        }

        /// <summary>Publishes a scheme this step cares about.</summary>
        protected abstract SchemaResult PublishScheme(SchemaPublishRequest request);
    }

    /// <summary>
    /// What a publish step is given: the scheme being published, as authored, and the context the publish runs under.
    /// A step that wants only the published columns and rows can ask for them with
    /// <see cref="DataScheme.CreatePublishedCopy"/>.
    /// </summary>
    public readonly struct SchemaPublishRequest
    {
        /// <summary>The context the publish runs under: driver, scheme, and the rest of the breadcrumb trail.</summary>
        public SchemaContext Context { get; }

        /// <summary>The scheme being published, as it is authored (every column, every row).</summary>
        public DataScheme Scheme { get; }

        /// <summary>The name of <see cref="Scheme"/>, for the common case of a step that handles one table.</summary>
        public string SchemeName => Scheme?.SchemeName;

        public SchemaPublishRequest(SchemaContext context, DataScheme scheme)
        {
            Context = context;
            Scheme = scheme;
        }
    }
}
