using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Demo
{
    public class AbstractConverter<TAbstract, TReal> : JsonConverter
    {
        public override Boolean CanConvert(Type objectType)
            => objectType == typeof(TAbstract);

        public override Object ReadJson(JsonReader reader, Type type, Object value, JsonSerializer jser)
            => jser.Deserialize<TReal>(reader);

        public override void WriteJson(JsonWriter writer, Object value, JsonSerializer jser)
            => jser.Serialize(writer, value);
    }
}
