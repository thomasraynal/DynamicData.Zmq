using System;

namespace DynamicData.Zmq.Shared
{
    public class ActorMonitoringError
    {
        public ActorErrorType CacheErrorStatus { get; internal set; }
        public Exception Exception { get; internal set; }

        public string Message => $"{CacheErrorStatus} - {Exception.Message}";

        public override string ToString()
        {
            return Message;
        }
    }
}
