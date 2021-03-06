﻿using DynamicData.Zmq.Aggregate;

namespace DynamicData.Zmq.Event
{
    public abstract class CommandBase<TKey, TAggregate> : EventBase<TKey, TAggregate> where TAggregate : IAggregate<TKey>
    {
        protected CommandBase()
        {
        }

        protected CommandBase(TKey aggregateId) : base(aggregateId)
        {
        }
    }
}
