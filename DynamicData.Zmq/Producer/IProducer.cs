using System;
using DynamicData.Event;
using DynamicData.Shared;

namespace DynamicData.Producer
{
    public interface IProducer<TKey, TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        ProducerState ProducerState { get; }

        IObservable<ProducerState> OnStateChanged { get; }
        void Publish(IEvent<TKey, TAggregate> @event);
    }
}