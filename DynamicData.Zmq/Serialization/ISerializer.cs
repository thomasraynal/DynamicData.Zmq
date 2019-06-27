using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Serialization
{
    public interface ISerializer
    {
        T Deserialize<T>(byte[] bytes);
        object Deserialize(byte[] bytes, Type type);
        byte[] Serialize(object obj);
    }
}
