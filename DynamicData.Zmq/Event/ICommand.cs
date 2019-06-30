using DynamicData.Zmq.Aggregate;

namespace DynamicData.Zmq.Event
{
    public interface ICommand<TKey, TAggregate> : IEvent<TKey, TAggregate> where TAggregate : IAggregate<TKey>
    {
    }
}
