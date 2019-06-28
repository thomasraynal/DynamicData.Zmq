using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace DynamicData.Shared
{
    public static class DisposableExtensions
    {
        public static void Cleanup(this IDisposable disposable, CompositeDisposable cleanup )
        {
            cleanup.Add(disposable);
        }

    }
}
