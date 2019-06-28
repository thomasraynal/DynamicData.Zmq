﻿using DynamicData;
using System;
using System.Collections.Generic;
using DynamicData.Shared;
using System.Collections.ObjectModel;

namespace DynamicData.Cache
{
    public interface IDynamicCache<TKey, TAggregate> : IActor where TAggregate : IAggregate<TKey>
    {
        IObservableCache<TAggregate, TKey> OnItemChanged { get; }
        IEnumerable<TAggregate> Items { get; }
        IObservable<DynamicCacheState> OnStateChanged { get; }
        DynamicCacheState CacheState { get; }
        bool IsStaled { get; }
        IObservable<bool> OnStaled { get; }
        bool IsCaughtingUp { get; }
        IObservable<bool> OnCaughtingUp { get; }
        ObservableCollection<DynamicCacheMonitoringError> Errors { get; }

    }
}
