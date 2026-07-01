namespace ZeroPlus.Oms.Clients
{
    public interface IDataSubscribers
    {
        event ValueUpdatedEventHandler ValueUpdatedEvent;

        void AddAndInitSubscriber(IOmsDataSubscriber subscriber);
        bool IsEmpty();
        bool Remove(IOmsDataSubscriber subscriber);
        void ResetValuesAsync();
        void UpdateValues(object value, bool isFromCache = false, bool allowCaching = true);
        bool SubscriptionInitialized { get; }
    }
}