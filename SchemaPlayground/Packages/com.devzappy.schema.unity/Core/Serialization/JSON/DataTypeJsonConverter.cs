using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class DataTypeJsonConverter : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new System.NotImplementedException($"{nameof(DataTypeJsonConverter)}.{nameof(WriteJson)}");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);

            Type resolvedType = null;

            // Prefer explicit $type when provided (legacy/newtonsoft type hint)
            var typeToken = jo[JsonUtils.PROPERTY_NAME_TYPE] as JValue;
            if (typeToken?.Type == JTokenType.String)
            {
                resolvedType = Type.GetType(typeToken.Value as string);
                if (resolvedType == null) // The object has a type but we don't know what it is in the current runtime, assume it's a plugin
                {
                    resolvedType = typeof(PluginDataType);
                }
            }
            else
            {
                // Newer format: optional TypeName discriminator
                if (jo.TryGetValue("TypeName", StringComparison.Ordinal, out var typeNameToken) &&
                    typeNameToken.Type == JTokenType.String)
                {
                    var typeName = typeNameToken.Value<string>();
                    var resolvedDataType = DataType.BuiltInTypes.FirstOrDefault(builtIn => builtIn.TypeName.Equals(typeName));
                    if (resolvedDataType != null)
                    {
                        resolvedType = resolvedDataType.GetType();
                    }
                    else if (!string.IsNullOrEmpty(typeName) && typeName.Contains(ReferenceDataType.TypeNamePrefix))
                    {
                        resolvedType = typeof(ReferenceDataType);
                    }
                }

                // Heuristic fallback by inspecting known property shapes when no discriminator is present
                if (resolvedType == null)
                {
                    // ReferenceDataType shape
                    if (jo.ContainsKey(nameof(ReferenceDataType.ReferenceSchemeName)) || 
                        jo.ContainsKey(nameof(ReferenceDataType.ReferenceAttributeName)) || 
                        jo.ContainsKey(nameof(ReferenceDataType.SupportsEmptyReferences)))
                    {
                        resolvedType = typeof(ReferenceDataType);
                    }
                    // FilePathDataType / FolderDataType share FSDataType props; default to FilePath
                    else if (jo.ContainsKey(nameof(FilePathDataType.AllowEmptyPath)) || 
                             jo.ContainsKey(nameof(FilePathDataType.UseRelativePaths)) || 
                             jo.ContainsKey(nameof(FilePathDataType.BasePath)))
                    {
                        resolvedType = typeof(FilePathDataType);
                    }
                    // Integer vs Text based on DefaultValue json type
                    else if (jo.TryGetValue(nameof(DataType.DefaultValue), StringComparison.Ordinal, out var defaultValueToken))
                    {
                        if (defaultValueToken.Type == JTokenType.Integer)
                        {
                            resolvedType = typeof(IntegerDataType);
                        }
                        else
                        {
                            // Treat strings and null as Text default (tests expect string default "")
                            resolvedType = typeof(TextDataType);
                        }
                    }
                }
            }

            if (resolvedType == null)
            {
                throw new JsonSerializationException($"DataTypeConverter: Unable to resolve underlying data type: {jo}");
            }

            return jo.ToObject(resolvedType, serializer);
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(DataType) == objectType;
        }
    }
}