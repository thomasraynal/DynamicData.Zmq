using DynamicData;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;

namespace ZeroMQPlayground.DynamicData.Shared
{
    public static class DynamicDataExtensions
    {
        public static IObservable<IChangeSet<TObject, TKey>> FilterEvents<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            return source.Filter(filter).WhereReasonsAreNot(ChangeReason.Remove);
        }

        public static IObservable<IChangeSet<TObject, TKey>> FilterEvents<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged)
        {
            return source.Filter(predicateChanged).WhereReasonsAreNot(ChangeReason.Remove);
        }

        public static IObservable<IChangeSet<TObject, TKey>> FilterEvents<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Unit> reapplyFilter)
        {
            return source.Filter(reapplyFilter).WhereReasonsAreNot(ChangeReason.Remove);
        }

        public static IObservable<IChangeSet<TObject, TKey>> FilterEvents<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged, IObservable<Unit> reapplyFilter)
        {
            return source.Filter(predicateChanged, reapplyFilter).WhereReasonsAreNot(ChangeReason.Remove);
        }

    }
}
