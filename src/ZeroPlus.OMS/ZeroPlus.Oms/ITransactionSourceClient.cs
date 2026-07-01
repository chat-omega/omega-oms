using System.Threading.Tasks;
using ZeroPlus.Oms.Subscription;

namespace ZeroPlus.Oms
{
    public delegate void ConnectionStatusChangedEventHandler(bool connected);

    public interface ITransactionSourceClient
    {
        bool IsConnected { get; set; }

        event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;
        void AddOrderUpdateSubscriber(IOrderUpdateSubscriber orderUpdateSubscriber);
        void RemoveOrderUpdateSubscriber(IOrderUpdateSubscriber orderBookViewModelBase);
        void AddTransactionSubscriber(ITransactionSubscriber transactionSubscriber);

        Task<bool> StartAsync();
        bool Start();
        Task StopAsync();
        void Stop();
    }
}