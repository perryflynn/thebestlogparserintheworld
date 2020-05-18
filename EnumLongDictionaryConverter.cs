using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace logsplit
{
    public class EnumLongDictionaryConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dictionary = value as Dictionary<AddressFamily, long>;

            writer.WriteStartObject();

            foreach (KeyValuePair<AddressFamily, long> pair in dictionary)
            {
                writer.WritePropertyName(Enum.GetName(typeof(AddressFamily), pair.Key));
                writer.WriteValue(pair.Value);
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            var dict = existingValue as Dictionary<AddressFamily, long>;

            foreach(var jEl in jObject)
            {
                AddressFamily keyEnum = (AddressFamily)Enum.Parse(typeof(AddressFamily), jEl.Key);
                long value = (long)jEl.Value;
                dict.Add(keyEnum, value);
            }

            return existingValue;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof (IDictionary<AddressFamily, long>) == objectType;
        }
    }
}
