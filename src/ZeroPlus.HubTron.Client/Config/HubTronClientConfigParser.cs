using System;
using System.Collections.Generic;
using ZeroPlus.HubTron.Client.Config.Interfaces;

namespace ZeroPlus.HubTron.Client.Config
{
    public class HubTronClientConfigParser : IHubTronClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "hubtron.config.json" };
        public IHubTronClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] HubTronClientConfigParser.Parse({configPath})");
            return new HubTronClientConfig();
        }
    }
}
