using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class DataTypeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            
            Type resolvedType = null;
            
            // check $type attribute for new underlying data type
            // legacy serialization but fast to check
            var type = jo["$type"];
            if (type != null)
            {
                resolvedType = Type.GetType(type.Value<string>());
            }
            else
            {
                string typeName = jo["TypeName"].Value<string>();

                var resolvedDataType = DataType.BuiltInTypes.FirstOrDefault(builtIn => builtIn.TypeName.Equals(typeName));
                
                if (resolvedDataType == null)
                {
                    if (typeName.Contains(ReferenceDataType.TypeNamePrefix))
                    {
                        resolvedType = typeof(ReferenceDataType);
                    }
                }
                else
                {
                    resolvedType = resolvedDataType.GetType();
                }
            }
            
            if (resolvedType != null)
            {
                return jo.ToObject(resolvedType, serializer);
            }
            else
            {
                throw new JsonSerializationException($"DataTypeConverter: Unable to resolve underlying data type: {jo}");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(DataType) == objectType;
        }
    }
}