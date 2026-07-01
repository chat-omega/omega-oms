using System;
using System.Collections.Generic;
using ZeroPlus.LiveVol.Client.Config.Interfaces;

namespace ZeroPlus.LiveVol.Client.Config
{
    public class LiveVolClientConfigParser : ILiveVolClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "livevol.config.json" };
        public ILiveVolClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] LiveVolClientConfigParser.Parse({configPath})");
            return new LiveVolClientConfig();
        }
    }
}
