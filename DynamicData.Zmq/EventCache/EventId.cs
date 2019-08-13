namespace DynamicData.Zmq.EventCache
{
    public readonly struct EventId
    {

        public EventId(string eventStream, long version, string subject, long timestamp)
        {
            EventStream = eventStream;
            Version = version;
            Subject = subject;
            Timestamp = timestamp;
        }

        public string EventStream { get;  }
        public long Version { get; }
        public string Subject { get;  }
        public string Id => $"{EventStream}.{Version}";

        public long Timestamp { get;  }

        public override bool Equals(object obj)
        {
            return obj is EventId id &&
                   Subject == id.Subject &&
                   Id == id.Id;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Subject.GetHashCode();
                hash = hash * 23 + Id.GetHashCode();
                return hash; 
            }
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
