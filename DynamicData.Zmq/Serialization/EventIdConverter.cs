using DynamicData.Zmq.EventCache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Serialization
{
    public class EventIdConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(EventId));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            var eventStream = (string)jsonObject["eventStream"];
            var version = (long)jsonObject["version"];
            var subject = (string)jsonObject["subject"];
            var timestamp = (long)jsonObject["timestamp"];

            var eventId = new EventId(eventStream, version, subject, timestamp);

            return eventId;
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
