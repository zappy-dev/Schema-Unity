using System.Text.RegularExpressions;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.Schemes;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

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
            
            // Note: value is only considered valid, if it is of the Color type.
            return CheckIf(value is Color, 
                errorMessage: "Value is not a valid color.",
                successMessage: "Value is a valid color", context);
        }

        /// <summary>
        /// A blank cell converted to a color is the type's default color, as a <see cref="Color"/>. The default is kept as hex text, but a
        /// cell of a Color column has to hold a Color to be valid.
        /// </summary>
        public override SchemaResult<object> ConvertEmptyValue(SchemaContext context)
        {
            using var _ = new DataTypeContextScope(ref context, this);

            if (DefaultValue is Color defaultColor)
            {
                return Pass<object>(result: defaultColor,
                    successMessage: "Converted empty data to default color", context);
            }

            var defaultText = DefaultValue?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(defaultText))
            {
                if (!defaultText.StartsWith("#"))
                {
                    defaultText = "#" + defaultText;
                }

                if (ColorUtility.TryParseHtmlString(defaultText, out Color parsedDefault))
                {
                    return Pass<object>(result: parsedDefault,
                        successMessage: "Converted empty data to default color", context);
                }
            }

            return Fail<object>($"An empty value cannot become a color: the default value '{DefaultValue}' of the {TypeName} type is not a color.", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this);
            
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ConvertEmptyValue(context);
            }
            
            string inputString = value.ToString();
            
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
            
            return Pass<object>(result: color,
                successMessage: $"Converted value to hex color", context);
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
        
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            Logger.LogVerbose("Initializing Color DataType");
            AddPluginType(new ColorDataType());
        }
    }
}