using System;
using System.Text.RegularExpressions;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Schemes;
using UnityEngine;
using static Schema.Core.SchemaResult;

namespace Schema.Runtime.Type
{
    /// <summary>
    /// Represents a color data type backed by a hex code string that returns Unity Engine Color values.
    /// Supports both 6-digit (#RRGGBB) and 8-digit (#RRGGBBAA) hex formats using Unity's ColorUtility.
    /// </summary>
    public class ColorDataType : DataType
    {
        private static readonly Regex HexColorRegex = new Regex(@"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);
        
        public override string TypeName => "Color";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => 
            SchemaResult<string>.Pass($"{nameof(EntryWrapper.DataEntry)}.GetDataAsColor(\"{attribute.AttributeName}\").Result");
        public override string CSDataType => typeof(Color).ToString();
        
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
            
            // Use Unity's ColorUtility for validation
            bool isValidHex = ColorUtility.TryParseHtmlString(hexString, out _);
            
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
            
            // Use Unity's ColorUtility for validation and conversion
            if (!ColorUtility.TryParseHtmlString(normalizedInput, out Color color))
            {
                return Fail<object>($"Failed to convert '{value}' to {TypeName}. Invalid hex color format. Expected #RRGGBB or #RRGGBBAA", context: context);
            }
            
            // Return the normalized hex string (Unity's ColorUtility normalizes it)
            string normalizedHex = ColorUtility.ToHtmlStringRGBA(color);
            if (normalizedHex.Length == 6) // If no alpha, don't include it
            {
                normalizedHex = normalizedHex.Substring(0, 6);
            }
            
            return Pass<object>(result: "#" + normalizedHex,
                successMessage: $"Converted '{value}' to hex color '#{normalizedHex}'", context);
        }
        
        /// <summary>
        /// Validates if a hex color string is in the correct format using Unity's ColorUtility.
        /// </summary>
        /// <param name="hexColor">The hex color string to validate</param>
        /// <returns>True if the format is valid, false otherwise</returns>
        public static bool IsValidHexColor(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
                return false;
                
            return ColorUtility.TryParseHtmlString(hexColor, out _);
        }
        
        /// <summary>
        /// Normalizes a hex color string using Unity's ColorUtility.
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
            
            if (ColorUtility.TryParseHtmlString(normalized, out Color color))
            {
                string normalizedHex = ColorUtility.ToHtmlStringRGBA(color);
                if (normalizedHex.Length == 6) // If no alpha, don't include it
                {
                    normalizedHex = normalizedHex.Substring(0, 6);
                }
                return "#" + normalizedHex;
            }
            
            return null;
        }
        
        /// <summary>
        /// Converts a hex color string to a Unity Color object.
        /// </summary>
        /// <param name="hexColor">The hex color string to convert</param>
        /// <returns>A Unity Color object, or Color.black if conversion fails</returns>
        public static Color HexToColor(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
                return Color.black;
                
            string normalized = hexColor.Trim();
            if (!normalized.StartsWith("#"))
            {
                normalized = "#" + normalized;
            }
            
            if (ColorUtility.TryParseHtmlString(normalized, out Color color))
            {
                return color;
            }
            
            return Color.black;
        }
        
        /// <summary>
        /// Converts a Unity Color object to a hex color string.
        /// </summary>
        /// <param name="color">The Unity Color object to convert</param>
        /// <param name="includeAlpha">Whether to include alpha channel in the hex string</param>
        /// <returns>A hex color string (e.g., "#FF0000" or "#FF0000AA")</returns>
        public static string ColorToHex(Color color, bool includeAlpha = false)
        {
            if (includeAlpha)
            {
                return "#" + ColorUtility.ToHtmlStringRGBA(color);
            }
            else
            {
                return "#" + ColorUtility.ToHtmlStringRGB(color);
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Logger.LogVerbose("Initializing Color DataType");
            AddPluginType(new ColorDataType());
        }
    }
}