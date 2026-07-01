using System;
using System.Net;
using System.Reflection;
using ZeroPlus.Interpolator.Client.Interfaces;

namespace ZeroPlus.Oms.Clients;

public class InterpolatorClient : ClientBase
{
    public IInterpolatorClient Client { get; private set; }

    public void Initialize(IInterpolatorClient client)
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