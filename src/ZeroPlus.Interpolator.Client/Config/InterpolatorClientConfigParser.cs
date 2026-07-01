using System;
using System.Collections.Generic;
using ZeroPlus.Interpolator.Client.Config.Interfaces;

namespace ZeroPlus.Interpolator.Client.Config
{
    public class InterpolatorClientConfigParser : IInterpolatorClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "interpolator.config.json" };
        public IInterpolatorClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] InterpolatorClientConfigParser.Parse({configPath})");
            return new InterpolatorClientConfig();
        }
    }
}
