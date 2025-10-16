namespace Schema.Core
{
    public static class SchemaContextFactory
    {
        public static SchemaContext CreateEditContext(string driver)
        {
            return SchemaContext.EditContext.WithDriver(driver);
        }

        public static SchemaContext CreateEditContext(SchemaContext ctx)
        {
            return SchemaContext.EditContext | ctx;
        }
        
        public static SchemaContext CreateRuntimeContext(string driver)
        {
            return SchemaContext.RuntimeContext.WithDriver(driver);
        }

        public static SchemaContext CreateRuntimeContext(SchemaContext ctx)
        {
            return SchemaContext.RuntimeContext | ctx;
        }
    }
}