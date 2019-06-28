using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Cache
{
    public class DynamicCacheMonitoringError
    {
        public DynamicCacheErrorType CacheErrorStatus { get; internal set; }
        public Exception Exception { get; internal set; }
    }
}
