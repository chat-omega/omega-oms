using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NLog;

namespace ZeroPlus.Oms.Clients;

public abstract class ClientBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

    protected static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    private bool _isConnected = false;

    public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

    public string Username => OmsCore?.User != null ? OmsCore.User.Username : "Excel";
    public string AppId => OmsCore?.User != null ? "ZeroPlus OMS APP" : "ZeroPlus OMS AddIn";

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            OnPropertyChanged();
        }
    }

    protected void OnClient_ClientConnected()
    {
        IsConnected = true;
        NotifyConnectionStatusChange();
        RegisterClient();
    }

    protected void OnClient_ClientDisconnected()
    {
        IsConnected = false;
        NotifyConnectionStatusChange();
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public async Task<bool> StartAsync()
    {
        return await Task.Run(Start);
    }

    public abstract bool Start();

    public async Task StopAsync()
    {
        await Task.Run(Stop);
    }

    public abstract void Stop();

    protected abstract void RegisterClient();

    protected void NotifyConnectionStatusChange()
    {
        ConnectionStatusChangedEvent?.Invoke(IsConnected);
    }

    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}