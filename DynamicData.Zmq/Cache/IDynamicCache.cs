using DynamicData;
using System;
using System.Collections.Generic;
using DynamicData.Shared;

namespace DynamicData.Cache
{
    public interface IDynamicCache<TKey, TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        IObservableCache<TAggregate, TKey> OnItemChanged { get; }
        IEnumerable<TAggregate> Items { get; }
        IObservable<DynamicCacheState> OnStateChanged { get; }
        DynamicCacheState CacheState { get; }
        IObservable<bool> OnStaled { get; }
        bool IsStaled { get; }
        IObservable<bool> OnCaughtingUp { get; }
        bool IsCaughtingUp { get; }
    }
}
