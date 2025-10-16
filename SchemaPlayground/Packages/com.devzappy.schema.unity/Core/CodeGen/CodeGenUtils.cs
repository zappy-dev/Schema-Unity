namespace Schema.Core.CodeGen
{
    public static class CodeGenUtils
    {
        public static readonly SchemaContext Context =
            SchemaContextFactory.CreateRuntimeContext(driver: "Codegen_Wrapper");
    }
}
