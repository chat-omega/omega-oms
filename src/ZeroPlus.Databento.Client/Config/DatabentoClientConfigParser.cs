using System;
using System.Collections.Generic;
using ZeroPlus.Databento.Client.Config.Interfaces;

namespace ZeroPlus.Databento.Client.Config
{
    public class DatabentoClientConfigParser : IDatabentoClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "databento.config.json" };
        public IDatabentoClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] DatabentoClientConfigParser.Parse({configPath})");
            return new DatabentoClientConfig();
        }
    }
}
