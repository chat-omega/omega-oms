
using System;
using System.Collections.Generic;
using ZeroPlus.SymbolMap.Client.Config.Interfaces;

namespace ZeroPlus.SymbolMap.Client.Config
{
    public class SymbolMapClientConfigParser : ISymbolMapClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "symbolmap.config.json" };
        public ISymbolMapClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] SymbolMapClientConfigParser.Parse({configPath})");
            return new SymbolMapClientConfig();
        }
    }
}
