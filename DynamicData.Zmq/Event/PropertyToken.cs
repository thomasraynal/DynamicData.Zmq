using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DynamicData.Event
{
    public class PropertyToken
    {
        public PropertyToken(int position, Type eventType, PropertyInfo propertyInfo)
        {
            Position = position;
            EventType = eventType;
            PropertyInfo = propertyInfo;
        }

        public int Position { get; }
        public PropertyInfo PropertyInfo { get; }
        public Type EventType { get; }

    }
}
