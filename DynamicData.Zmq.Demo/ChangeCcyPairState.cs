using DynamicData.Event;

namespace DynamicData.Demo
{
    public class ChangeCcyPairState : CommandBase<string, CurrencyPair>
    {
        [RoutingPosition(0)]
        public CcyPairState State { get; set; }

        [RoutingPosition(1)]
        public string Market { get; set; }

        public override void Apply(CurrencyPair aggregate)
        {
            aggregate.State = State;
        }
    }
}
