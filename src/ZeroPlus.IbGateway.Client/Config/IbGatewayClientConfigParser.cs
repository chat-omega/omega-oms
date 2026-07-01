using System;
using System.Collections.Generic;
using ZeroPlus.IbGateway.Client.Config.Interfaces;

namespace ZeroPlus.IbGateway.Client.Config
{
    public class IbGatewayClientConfigParser : IIbGatewayClientConfigParser
    {
        public List<string> GetSavedConfigsList() => new() { "ibgateway.config.json" };
        public IIbGatewayClientConfig Parse(string configPath)
        {
            Console.WriteLine($"[STUB] IbGatewayClientConfigParser.Parse({configPath})");
            return new IbGatewayClientConfig();
        }
    }
}
