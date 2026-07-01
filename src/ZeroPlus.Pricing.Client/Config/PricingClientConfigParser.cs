using System;
using System.Collections.Generic;
using ZeroPlus.Pricing.Client.Config.Interfaces;

namespace ZeroPlus.Pricing.Client.Config
{
    public class PricingClientConfigParser : IPricingClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "pricing.config.json" };
        public IPricingClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] PricingClientConfigParser.Parse({configPath})");
            return new PricingClientConfig();
        }
    }
}
