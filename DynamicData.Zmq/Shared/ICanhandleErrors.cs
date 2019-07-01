using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DynamicData.Zmq.Shared
{
    public interface ICanHandleErrors
    {
        ObservableCollection<ActorMonitoringError> Errors { get; }
    }
}
