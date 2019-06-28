using System;
using System.Runtime.Serialization;

namespace DynamicData.Cache
{
    [Serializable]
    public class UnreachableBrokerException : Exception
    {
        public UnreachableBrokerException()
        {
        }

        public UnreachableBrokerException(string message) : base(message)
        {
        }

        public UnreachableBrokerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected UnreachableBrokerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}