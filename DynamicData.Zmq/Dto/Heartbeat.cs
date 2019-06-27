using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Dto
{
    public class Heartbeat
    {
        public static readonly Heartbeat Query = new Heartbeat() { Type = HeartbeatType.Ping };

        public static readonly Heartbeat Response = new Heartbeat() { Type = HeartbeatType.Pong };

        public HeartbeatType Type { get; set; }

    }
}
