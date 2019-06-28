using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Shared
{
    public interface ICleanupable
    {
        void Cleanup(IDisposable disposable);
    }
}
