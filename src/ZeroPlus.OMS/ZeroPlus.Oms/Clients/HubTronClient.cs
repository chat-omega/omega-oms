using System;
using System.Net;
using System.Reflection;
using ZeroPlus.HubTron.Client.Interfaces;

namespace ZeroPlus.Oms.Clients;

public class HubTronClient : ClientBase
{
    public IHubTronClient Client { get; private set; }

    public void Initialize(IHubTronClient client)
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

    public override bool Start() => Client?.ConnectAndStart() ?? false;
    public override void Stop() => Client?.DisconnectAndStop();
}
