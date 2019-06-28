using DynamicData.Producer;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Demo
{
    public class MarketConfiguration : ProducerConfiguration
    {
        public string Name { get; set; }
        public TimeSpan PriceGenerationDelay { get; set; }
        public bool IsAutoGen { get; set; }

        public MarketConfiguration(string name): this()
        {
            Name = name;
        }

        public MarketConfiguration()
        {
            PriceGenerationDelay = TimeSpan.FromSeconds(1);
            IsAutoGen = true;
        }
    }
}
