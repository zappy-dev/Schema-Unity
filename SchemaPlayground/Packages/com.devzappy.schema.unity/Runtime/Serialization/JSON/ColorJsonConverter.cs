using Newtonsoft.Json;
using Schema.Core.Serialization;
using Schema.Runtime.Type;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Schema.Runtime.Serialization.JSON
{
    public class ColorJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var color = value as Color?;
            if (color == null)
            {
                return;
            }

            // Write the alpha channel when the color is not fully opaque, so it survives a save. Opaque colors keep the six digit form
            // that existing files hold. The alpha is judged at the 8 bits that the hex text can hold, so 0.999 is opaque.
            var isOpaque = Mathf.RoundToInt(color.Value.a * 255f) >= 255;
            var hexColor = ColorDataType.ColorToHex(color.Value, includeAlpha: !isOpaque);
            writer.WriteValue(hexColor);
        }

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new System.NotImplementedException();
        }

        public override bool CanConvert(System.Type objectType)
        {
            return objectType == typeof(Color);
        }
        
        public override bool CanRead => false;
        public override bool CanWrite => true;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void InitializeOnLoad()
        {
            JsonSettings.AddConverters(new ColorJsonConverter());
        }
    }
}