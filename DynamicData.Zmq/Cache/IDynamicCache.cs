using DynamicData;
using System;
using System.Collections.Generic;
using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData.Cache
{
    public interface IDynamicCache<TKey, TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        IObservableCache<TAggregate, TKey> OnItemChanged();
        IEnumerable<TAggregate> GetItems();
        IObservable<DynamicCacheState> OnStateChanged();
        DynamicCacheState CacheState { get; }
        IObservable<bool> OnStaled();
        bool IsStaled { get; }
        bool IsCaughtingUp { get; }
    }
}
