using System;

namespace DynamicData.Zmq.Cache
{
    public class DynamicCacheMonitoringError
    {
        public DynamicCacheErrorType CacheErrorStatus { get; internal set; }
        public Exception Exception { get; internal set; }

        public string Message => $"{CacheErrorStatus} - {Exception.Message}";

        public override string ToString()
        {
            return Message;
        }
    }
}
