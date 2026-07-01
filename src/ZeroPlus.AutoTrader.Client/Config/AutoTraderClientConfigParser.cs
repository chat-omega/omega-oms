
using System;
using System.Collections.Generic;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;

namespace ZeroPlus.AutoTrader.Client.Config
{
    public class AutoTraderClientConfigParser : IAutoTraderClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "autotrader.config.json" };
        public IAutoTraderClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] AutoTraderClientConfigParser.Parse({configPath})");
            return new AutoTraderClientConfig();
        }
    }
}
