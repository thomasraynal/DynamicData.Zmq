using System;

namespace DynamicData.Zmq.Serialization
{
    public interface ISerializer
    {
        T Deserialize<T>(byte[] bytes);
        object Deserialize(byte[] bytes, Type type);
        byte[] Serialize(object obj);
    }
}
