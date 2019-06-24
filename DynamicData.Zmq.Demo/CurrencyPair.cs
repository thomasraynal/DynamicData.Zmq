using System.Linq;
using ZeroMQPlayground.DynamicData.Aggregate;

namespace ZeroMQPlayground.DynamicData.Demo
{
    public class CurrencyPair : AggregateBase<string>
    {

        public CcyPairState State { get; set; }

        public double Ask { get; set; }
        public double Bid { get; set; }
        public double Mid { get; set; }
        public double Spread { get; set; }

        public override string ToString()
        {
            return $"{this.Id}({AppliedEvents.Count()} event(s))";
        }
    }
}
