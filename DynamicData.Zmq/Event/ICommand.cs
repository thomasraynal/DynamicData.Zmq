using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Event
{
    public interface ICommand<TKey, TAggregate> : IEvent<TKey, TAggregate> where TAggregate : IAggregate<TKey>
    {
    }
}
