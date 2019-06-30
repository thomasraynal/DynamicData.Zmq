using System;

namespace DynamicData.Zmq.Shared
{
    public interface ICleanupable
    {
        void Cleanup(IDisposable disposable);
    }
}
