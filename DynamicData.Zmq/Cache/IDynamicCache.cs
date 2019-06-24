using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using ZeroMQPlayground.DynamicData.Shared;

namespace ZeroMQPlayground.DynamicData.Cache
{
    public interface IDynamicCache<TKey,TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        IObservableCache<TAggregate, TKey> OnItemChanged();
        IEnumerable<TAggregate> GetItems();
        IObservable<DynamicCacheState> OnStateChanged();
        DynamicCacheState CacheState { get; }
    }
}
