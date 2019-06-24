using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Event
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class Routable : Attribute
    {
    }
}
