namespace Schema.Core.Tests.Ext;

public static class SchemaResultExt
{
    public static void AssertFailed(this SchemaResult result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Failed, result.ToString());
    }
    
    public static void AssertPassed(this SchemaResult result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Passed, result.ToString());
    }

    public static void AssertCondition(this SchemaResult result, bool condition)
    {
        Assert.NotNull(result);
        Assert.That(result.Passed, Is.EqualTo(condition), result.ToString());
    }
    
    public static void AssertFailed<TRes>(this SchemaResult<TRes> result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Failed, result.ToString());
    }
    
    public static void AssertPassed<TRes>(this SchemaResult<TRes> result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Passed, result.ToString());
    }

    public static void AssertCondition<TRes>(this SchemaResult<TRes> result, bool condition)
    {
        Assert.NotNull(result);
        Assert.That(result.Passed, Is.EqualTo(condition), result.ToString());
    }

    public static void AssertCondition<TRes>(this SchemaResult<TRes> result, bool condition, object payload)
    {
        Assert.NotNull(result);
        Assert.That(result.Passed, Is.EqualTo(condition), result.ToString());
        if (condition)
        {
            Assert.That(result.Result, Is.EqualTo(payload));
        }
    }

    public static void TryAssert<TRes>(this SchemaResult<TRes> result, out TRes payload)
    {
        Assert.NotNull(result);
        bool success = result.Try(out payload);
        Assert.IsTrue(success, result.ToString());
    }
}