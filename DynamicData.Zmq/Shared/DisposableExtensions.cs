using System;
using System.Reactive.Disposables;

namespace DynamicData.Zmq.Shared
{
    public static class DisposableExtensions
    {
        public static void Cleanup(this IDisposable disposable, CompositeDisposable cleanup )
        {
            cleanup.Add(disposable);
        }

    }
}
