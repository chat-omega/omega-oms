using System;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ZeroPlus.Telemetry.Client.Interfaces;

namespace ZeroPlus.Oms.Clients;

public class TelemetryClient : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

    public ITelemetryClient Client { get; private set; }
    private bool _isConnected;

    public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            OnPropertyChanged();
        }
    }

    public void Initialize(ITelemetryClient client)
    {
        Client = client;
        Client.ClientConnected += OnClient_ClientConnected;
        Client.ClientDisconnected += OnClient_ClientDisconnected;
    }

    #region PublicMethods

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public async Task<bool> StartAsync()
    {
        await Task.Run(() =>
        {
            Client?.ConnectAndStart();
        });
        return false;
    }

    public async Task StopAsync()
    {
        await Task.Run(() =>
        {
            Client?.DisconnectAndStop();
        });
    }
    #endregion

    private void OnClient_ClientDisconnected()
    {
        IsConnected = false;
        ConnectionStatusChangedEvent?.Invoke(IsConnected);
    }

    private void OnClient_ClientConnected()
    {
        IsConnected = true;
        ConnectionStatusChangedEvent?.Invoke(IsConnected);
        if (IsConnected)
        {
            RegisterClient();
        }
    }

    private void RegisterClient()
    {
        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        if (OmsCore.User != null)
        {
            Client.RegisterClient(OmsCore.User.Username, "ZeroPlus OMS App", version!, Dns.GetHostName());
        }
        else
        {
            Client.RegisterClient("Excel", "ZeroPlus OMS AddIn", version!, Dns.GetHostName());
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
