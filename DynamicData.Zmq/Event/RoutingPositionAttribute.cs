using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Event
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
