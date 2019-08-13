using Newtonsoft.Json;
using System;
using System.Text;
using DynamicData.Zmq.Serialization;

namespace DynamicData.Zmq.Default
{
    public class JsonNetSerializer : ISerializer
    {
        private static readonly EventIdConverter _eventIdConverter = new EventIdConverter();

        public T Deserialize<T>(byte[] bytes)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes), _eventIdConverter);
        }

        public object Deserialize(byte[] bytes, Type type)
        {
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bytes), type, _eventIdConverter);
        }

        public byte[] Serialize(object obj)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
        }
    }
}
