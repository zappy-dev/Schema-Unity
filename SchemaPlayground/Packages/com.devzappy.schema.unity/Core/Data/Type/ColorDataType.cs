using System;
using System.Text.RegularExpressions;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    /// <summary>
    /// Represents a color data type backed by a hex code string.
    /// Supports both 6-digit (#RRGGBB) and 8-digit (#RRGGBBAA) hex formats.
    /// </summary>
    [Serializable]
    public class ColorDataType : DataType
    {
        private static readonly Regex HexColorRegex = new Regex(@"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);
        
        public override string TypeName => "Color";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => 
            SchemaResult<string>.Pass($"{nameof(DataEntry)}.{nameof(DataEntry.GetDataAsString)}(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(string).ToString();
        
        public override object Clone()
        {
            return new ColorDataType
            {
                DefaultValue = DefaultValue
            };
        }

        public ColorDataType() : base("#000000")
        {
            
        }

        public ColorDataType(string hexColor) : base(hexColor)
        {
            
        }
        
        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            
            if (value == null)
            {
                return CheckIf(false, 
                    errorMessage: "Color value cannot be null",
                    successMessage: "Color value is valid", context);
            }
            
            string hexString = value.ToString();
            
            if (string.IsNullOrWhiteSpace(hexString))
            {
                return CheckIf(false, 
                    errorMessage: "Color value cannot be empty or whitespace",
                    successMessage: "Color value is valid", context);
            }
            
            bool isValidHex = HexColorRegex.IsMatch(hexString);
            
            return CheckIf(isValidHex, 
                errorMessage: $"Value '{hexString}' is not a valid hex color format. Expected #RRGGBB or #RRGGBBAA",
                successMessage: $"Value '{hexString}' is a valid hex color", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            
            if (value == null)
            {
                return Pass<object>(result: "#000000",
                    successMessage: "Converted null to default color", context);
            }
            
            string inputString = value.ToString();
            
            if (string.IsNullOrWhiteSpace(inputString))
            {
                return Pass<object>(result: "#000000",
                    successMessage: "Converted empty string to default color", context);
            }
            
            // Normalize the input by ensuring it starts with #
            string normalizedInput = inputString.Trim();
            if (!normalizedInput.StartsWith("#"))
            {
                normalizedInput = "#" + normalizedInput;
            }
            
            // Validate the normalized input
            if (!HexColorRegex.IsMatch(normalizedInput))
            {
                return Fail<object>($"Failed to convert '{value}' to {TypeName}. Invalid hex color format. Expected #RRGGBB or #RRGGBBAA", context: context);
            }
            
            return Pass<object>(result: normalizedInput.ToUpperInvariant(),
                successMessage: $"Converted '{value}' to hex color '{normalizedInput.ToUpperInvariant()}'", context);
        }
        
        /// <summary>
        /// Validates if a hex color string is in the correct format.
        /// </summary>
        /// <param name="hexColor">The hex color string to validate</param>
        /// <returns>True if the format is valid, false otherwise</returns>
        public static bool IsValidHexColor(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
                return false;
                
            return HexColorRegex.IsMatch(hexColor);
        }
        
        /// <summary>
        /// Normalizes a hex color string by ensuring it starts with # and is uppercase.
        /// </summary>
        /// <param name="hexColor">The hex color string to normalize</param>
        /// <returns>The normalized hex color string, or null if invalid</returns>
        public static string NormalizeHexColor(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
                return null;
                
            string normalized = hexColor.Trim();
            if (!normalized.StartsWith("#"))
            {
                normalized = "#" + normalized;
            }
            
            return HexColorRegex.IsMatch(normalized) ? normalized.ToUpperInvariant() : null;
        }
    }
}