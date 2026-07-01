using System;
using System.Collections.Generic;
using ZeroPlus.Ema.Client.Config.Interfaces;

namespace ZeroPlus.Ema.Client.Config
{
    public class EmaClientConfigParser : IEmaClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "ema.config.json" };
        public IEmaClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] EmaClientConfigParser.Parse({configPath})");
            return new EmaClientConfig();
        }
    }
}
