using System;

namespace DynamicData.Zmq.Event
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class Routable : Attribute
    {
    }
}
