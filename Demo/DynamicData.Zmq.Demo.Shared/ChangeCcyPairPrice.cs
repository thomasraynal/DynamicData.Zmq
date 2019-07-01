using DynamicData.Zmq.Event;

namespace DynamicData.Zmq.Demo
{
    public class ChangeCcyPairPrice : CommandBase<string, CurrencyPair>
    {
        public ChangeCcyPairPrice(string ccyPairId, string market, double ask, double bid, double mid, double spread): base(ccyPairId)
        {
            Ask = ask;
            Bid = bid;
            Mid = mid;
            Spread = spread;
            Market = market;
        }

        public double Ask { get; set; }
        public double Bid { get; set; }
        public double Mid { get; set; }
        public double Spread { get; set; }

        [RoutingPosition(0)]
        public string Market { get; set; }

        public override void Apply(CurrencyPair aggregate)
        {
            aggregate.Ask = Ask;
            aggregate.Bid = Bid;
            aggregate.Mid = Mid;
            aggregate.Spread = Spread;
        }
    }
}
