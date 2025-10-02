    namespace Schema.Core.Tests.Ext;

/// <summary>
/// Extension methods for asserting conditions on SchemaResult objects in unit tests.
/// </summary>
public static class SchemaResultExt
{
    /// <summary>
    /// Asserts that a SchemaResult has failed.
    /// </summary>
    /// <param name="result">The Schema result to check.</param>
    /// <exception cref="AssertionException">Thrown when the result is null or has not failed.</exception>
    public static void AssertFailed(this SchemaResult result, string errorMessage = "")
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Failed, result.ToString());

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Assert.That(result.Message, Is.EqualTo(errorMessage));
        }
    }
    
    /// <summary>
    /// Asserts that a SchemaResult has passed.
    /// </summary>
    /// <param name="result">The Schema result to check.</param>
    /// <exception cref="AssertionException">Thrown when the result is null or has not passed.</exception>
    public static void AssertPassed(this SchemaResult result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Passed, result.ToString());
    }

    /// <summary>
    /// Asserts that a SchemaResult's pass status matches the expected condition.
    /// </summary>
    /// <param name="result">The Schema result to check.</param>
    /// <param name="condition">The expected pass status.</param>
    /// <exception cref="AssertionException">Thrown when the result is null or its pass status doesn't match the condition.</exception>
    public static void AssertCondition(this SchemaResult result, bool condition)
    {
        Assert.NotNull(result);
        Assert.That(result.Passed, Is.EqualTo(condition), result.ToString());
    }
    
    /// <summary>
    /// Asserts that a generic SchemaResult has failed.
    /// </summary>
    /// <typeparam name="TRes">The type of the result payload.</typeparam>
    /// <param name="result">The generic Schema result to check.</param>
    /// <exception cref="AssertionException">Thrown when the result is null or has not failed.</exception>
    public static void AssertFailed<TRes>(this SchemaResult<TRes> result)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Failed, result.ToString());
    }

    /// <summary>
    /// Asserts that a generic SchemaResult has passed and returns its result payload.
    /// </summary>
    /// <typeparam name="TRes">The type of the result payload.</typeparam>
    /// <param name="result">The generic Schema result to check.</param>
    /// <param name="expectedValue"></param>
    /// <returns>The result payload if the assertion passes.</returns>
    /// <exception cref="AssertionException">Thrown when the result is null or has not passed.</exception>
    public static TRes AssertPassed<TRes>(this SchemaResult<TRes> result, TRes? expectedValue = default)
    {
        Assert.NotNull(result);
        Assert.IsTrue(result.Passed, result.ToString());

        if (expectedValue != null)
        {
            Assert.That(result.Result, Is.EqualTo(expectedValue));
        }
        return result.Result;
    }

    /// <summary>
    /// Asserts that a generic SchemaResult's pass status matches the expected condition.
    /// </summary>
    /// <typeparam name="TRes">The type of the result payload.</typeparam>
    /// <param name="result">The generic Schema result to check.</param>
    /// <param name="condition">The expected pass status.</param>
    /// <exception cref="AssertionException">Thrown when the result is null or its pass status doesn't match the condition.</exception>
    public static void AssertCondition<TRes>(this SchemaResult<TRes> result, bool condition)
    {
        Assert.NotNull(result);
        Assert.That(result.Passed, Is.EqualTo(condition), result.ToString());
    }

    /// <summary>
    /// Asserts that a generic SchemaResult's pass status matches the expected condition and its payload equals the expected value.
    /// </summary>
    /// <typeparam name="TRes">The type of the result payload.</typeparam>
    /// <param name="result">The generic Schema result to check.</param>
    /// <param name="condition">The expected pass status.</param>
    /// <param name="payload">The expected payload value to compare against.</param>
    /// <exception cref="AssertionException">Thrown when the result is null, its pass status doesn't match the condition, or the payload doesn't match.</exception>
    public static void AssertCondition<TRes>(this SchemaResult<TRes> result, bool condition, object payload)
    {
        Assert.NotNull(result);
        Assert.That(result.Passed, Is.EqualTo(condition), result.ToString());
        if (condition)
        {
            Assert.That(result.Result, Is.EqualTo(payload));
        }
    }

    /// <summary>
    /// Attempts to get the result payload and asserts the operation was successful.
    /// </summary>
    /// <typeparam name="TRes">The type of the result payload.</typeparam>
    /// <param name="result">The generic Schema result to check.</param>
    /// <param name="payload">When this method returns, contains the result payload if successful; otherwise, the default value.</param>
    /// <returns>True if the payload was successfully retrieved; otherwise, false.</returns>
    /// <exception cref="AssertionException">Thrown when the result is null or the Try operation fails.</exception>
    public static bool TryAssert<TRes>(this SchemaResult<TRes> result, out TRes payload)
    {
        Assert.NotNull(result);
        bool success = result.Try(out payload);
        Assert.IsTrue(success, result.ToString());
        return success;
    }

    /// <summary>
    /// Attempts to get the result payload and asserts the operation's success matches the expected condition.
    /// </summary>
    /// <typeparam name="TRes">The type of the result payload.</typeparam>
    /// <param name="result">The generic Schema result to check.</param>
    /// <param name="condition">The expected success status of the Try operation.</param>
    /// <param name="payload">When this method returns, contains the result payload if successful; otherwise, the default value.</param>
    /// <returns>True if the payload was successfully retrieved; otherwise, false.</returns>
    /// <exception cref="AssertionException">Thrown when the result is null or the Try operation's success doesn't match the condition.</exception>
    public static bool TryAssertCondition<TRes>(this SchemaResult<TRes> result, bool condition, out TRes payload)
    {
        Assert.NotNull(result);
        bool success = result.Try(out payload);
        Assert.That(success, Is.EqualTo(condition), result.ToString());
        return success;
    }
}