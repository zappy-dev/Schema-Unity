namespace Schema.Core
{
    public class ProjectContextScope: IContextScope
    {
        public SchemaContext Context { get; set; }
        
        public ProjectContextScope(ref SchemaContext context, SchemaProjectContainer project)
        {
            this.Context = context;
            if (Context != null)
            {
                Context.Project = project;
            }
        }

        public void Dispose()
        {
            if (Context != null)
            {
                Context.Project = null;
            }
            Context = null;
        }
    }
}