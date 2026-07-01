using System;
using System.Net;
using System.Reflection;
using ZeroPlus.Ema.Client.Interfaces;

namespace ZeroPlus.Oms.Clients
{
    public class FullEmaClient : ClientBase
    {
        public IEmaClient Client { get; private set; }

        public void Initialize(IEmaClient client)
        {
            Client = client;
            Client.ClientConnected += OnClient_ClientConnected;
            Client.ClientDisconnected += OnClient_ClientDisconnected;
        }

        protected override void RegisterClient()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Client.RegisterClient(Username, AppId, version!, Dns.GetHostName());
        }

        public override bool Start()
        {
            return Client?.ConnectAndStart(openSharedMemory: false) ?? false;
        }

        public override void Stop()
        {
            Client?.DisconnectAndStop();
        }
    }
}
