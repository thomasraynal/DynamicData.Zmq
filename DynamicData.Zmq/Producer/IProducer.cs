using System;
using DynamicData.Zmq.Aggregate;
using DynamicData.Zmq.Event;
using DynamicData.Zmq.Shared;

namespace DynamicData.Zmq.Producer
{
    public interface IProducer<TKey, TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        ProducerState ProducerState { get; }

        IObservable<ProducerState> OnStateChanged { get; }
        void Publish(IEvent<TKey, TAggregate> @event);
    }
}