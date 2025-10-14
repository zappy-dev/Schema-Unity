using Newtonsoft.Json;
using Schema.Core.Serialization;
using Schema.Runtime.Type;
using UnityEditor;
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

            var hexColor = ColorDataType.ColorToHex(color.Value);
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

        [InitializeOnLoadMethod]
        internal static void InitializeOnLoad()
        {
            JsonSettings.AddConverters(new ColorJsonConverter());
        }
    }
}