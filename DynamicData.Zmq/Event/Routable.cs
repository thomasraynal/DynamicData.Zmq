using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Event
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class Routable : Attribute
    {
    }
}
