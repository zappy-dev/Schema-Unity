namespace Schema.Core.Tests.Ext;

public static class SchemaResultExt
{
    public static void AssertFailed(this SchemaResult result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Failed);
    }
    
    public static void AssertSuccess(this SchemaResult result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.IsSuccess);
    }
}