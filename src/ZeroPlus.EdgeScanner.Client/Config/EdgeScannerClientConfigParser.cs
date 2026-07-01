
using System;
using System.Collections.Generic;
using ZeroPlus.EdgeScanner.Client.Config.Interfaces;

namespace ZeroPlus.EdgeScanner.Client.Config
{
    public class EdgeScannerClientConfigParser : IEdgeScannerClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "edgescanner.config.json" };
        public IEdgeScannerClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] EdgeScannerClientConfigParser.Parse({configPath})");
            return new EdgeScannerClientConfig();
        }
    }
}
