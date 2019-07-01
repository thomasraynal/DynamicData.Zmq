using System;
using System.Collections.Generic;
using DynamicData.Zmq.Shared;
using System.Collections.ObjectModel;
using DynamicData.Zmq.Aggregate;

namespace DynamicData.Zmq.Cache
{
    public interface IDynamicCache<TKey, TAggregate> : IActor, ICanHandleErrors where TAggregate : IAggregate<TKey>
    {
        IObservableCache<TAggregate, TKey> OnItemChanged { get; }
        IEnumerable<TAggregate> Items { get; }
        IObservable<DynamicCacheState> OnStateChanged { get; }
        DynamicCacheState CacheState { get; }
        bool IsStaled { get; }
        IObservable<bool> OnStaled { get; }
        bool IsCaughtingUp { get; }
        IObservable<bool> OnCaughtingUp { get; }
    }
}
