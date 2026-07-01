
using System;
using System.Collections.Generic;
using ZeroPlus.Trades.Client.Config.Interfaces;

namespace ZeroPlus.Trades.Client.Config
{
    public class TradesClientConfigParser : ITradesClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "trades.config.json" };
        public ITradesClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] TradesClientConfigParser.Parse({configPath})");
            return new TradesClientConfig();
        }
    }
}
