namespace ZeroPlus.Oms.Clients
{

    public interface IOmsDataSubscriber
    {
        bool IsDisposed { get; set; }
        void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false);
    }
}