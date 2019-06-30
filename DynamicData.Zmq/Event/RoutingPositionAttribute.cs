using System;

namespace DynamicData.Zmq.Event
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RoutingPositionAttribute : Attribute
    {
        public int Position { get; }

        public RoutingPositionAttribute(int position)
        {
            Position = position;
        }
    }
}
