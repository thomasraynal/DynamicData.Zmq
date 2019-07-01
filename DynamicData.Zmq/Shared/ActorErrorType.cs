namespace DynamicData.Zmq.Shared
{
    public enum ActorErrorType
    {
        None,
        DynamicCacheGetStateOfTheWorldFailure,
        DynamicCacheEventHandlingFailure,
        BrokerServiceEventProcessingFailure,
        BrokerServiceStateOfTheWorldRequestHandling
    }
}
