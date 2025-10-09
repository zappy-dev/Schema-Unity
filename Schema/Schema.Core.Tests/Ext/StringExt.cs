namespace Schema.Core.Tests.Ext;

/// <summary>
/// Extension methods for strings.
/// </summary>
public static class StringExt
{
    /// <summary>
    /// Sanitizes a string by removing all whitespace and line endings.
    /// </summary>
    /// <param name="str">The string to sanitize.</param>
    /// <returns>The sanitized string.</returns>
    public static string SanitizeWhitespace(this string str)
    {
        return str.ReplaceLineEndings(String.Empty)
            .Replace("\t", String.Empty)
            .Replace(" ", String.Empty);
    }
}