using System;
using System.Linq;

namespace Schema.Core.Ext
{
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a string to PascalCase by removing word delimiters and capitalizing each word.
        /// </summary>
        public static string ToPascalCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Split on spaces, underscores, dashes, dots, etc.
            var words = input
                .Trim()
                .Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);

            // Capitalize first letter of each word, lowercase the rest
            var transformed = words
                .Select(w => 
                    char.ToUpperInvariant(w[0]) 
                    + (w.Length > 1 
                        ? w.Substring(1)
                        : string.Empty));

            return string.Concat(transformed);
        }

        /// <summary>
        /// Converts a string to lowerCamelCase: like PascalCase but first character is lowercase.
        /// </summary>
        public static string ToCamelCase(this string input)
        {
            var pascal = input.ToPascalCase();
            if (string.IsNullOrEmpty(pascal) || char.IsLower(pascal[0]))
                return pascal;

            return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        }

        public static string[] SplitByLineEndings(this string input)
        {
            return input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Forces line endings to Windows-style line ending
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ReplaceLineEndings(this string input)
        {
            return input.Replace("\r\n", "\n");
        }
    }
}