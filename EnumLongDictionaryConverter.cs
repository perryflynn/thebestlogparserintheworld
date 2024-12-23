using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace logsplit
{
    public class EnumLongDictionaryConverter<TEnum> : JsonConverter
        where TEnum :  struct, IConvertible
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dictionary = value as Dictionary<TEnum, long>;

            writer.WriteStartObject();

            foreach (KeyValuePair<TEnum, long> pair in dictionary)
            {
                writer.WritePropertyName(Enum.GetName(typeof(TEnum), pair.Key));
                writer.WriteValue(pair.Value);
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            var dict = existingValue as Dictionary<TEnum, long>;

            foreach(var jEl in jObject)
            {
                TEnum keyEnum = (TEnum)Enum.Parse(typeof(TEnum), jEl.Key);
                long value = (long)jEl.Value;
                dict.Add(keyEnum, value);
            }

            return existingValue;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof (IDictionary<TEnum, long>) == objectType;
        }
    }
}
