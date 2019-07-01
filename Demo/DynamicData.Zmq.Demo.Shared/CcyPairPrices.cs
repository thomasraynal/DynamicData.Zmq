using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicData.Zmq.Demo.Shared
{
    public class Price
    {
        public string CcyPair { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public int EventCount { get; set; }
    }

    public class CcyPairPrices
    {
        public static readonly CcyPairPrices Default = new CcyPairPrices();
  
        public CcyPairPrices()
        {
            Prices = new List<Price>();
        }

        public List<Price> Prices { get; set; }

        public override string ToString()
        {
            if (Prices.Count == 0) return string.Empty;

            return Prices.Select(price => $"{price.CcyPair} [{price.Bid}/{price.Ask}] [{price.EventCount} event(s)]")
                         .Aggregate((p1, p2) => $"{p1}\n{p2}");
        }

    }
}
