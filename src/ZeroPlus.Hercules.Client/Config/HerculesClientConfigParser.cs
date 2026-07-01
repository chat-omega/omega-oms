
using System;
using System.Collections.Generic;
using ZeroPlus.Hercules.Client.Config.Interfaces;

namespace ZeroPlus.Hercules.Client.Config
{
    public class HerculesClientConfigParser : IHerculesClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "hercules.config.json" };
        public IHerculesClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] HerculesClientConfigParser.Parse({configPath})");
            return new HerculesClientConfig();
        }
    }
}
