using System;
using System.Collections.Generic;
using System.Text;
using DynamicData.Event;

namespace DynamicData.Aggregate
{
    public class AggregateBase<TKey> : IAggregate<TKey>
    {
        private readonly List<IEvent> _appliedEvents;

        public AggregateBase()
        {
            _appliedEvents = new List<IEvent>();

            Version = -1;
        }

        public TKey Id { get; set; }

        public IEnumerable<IEvent> AppliedEvents => _appliedEvents;

        public int Version { get; set; }

        public void Apply(IEvent @event)
        {
            if (!@event.CanApply(GetType())) throw new Exception($"cant apply to {this.GetType()}");

            @event.Apply((dynamic)this);

            OnEventApplied(@event);
        }

        public void Apply<TAggregate>(IEvent<TKey, TAggregate> @event) where TAggregate : IAggregate<TKey>
        {
            @event.Apply((dynamic)this);

            OnEventApplied(@event);
        }

        private void OnEventApplied(IEvent @event)
        {
            _appliedEvents.Add(@event);
            Version++;
        }
    }
}
