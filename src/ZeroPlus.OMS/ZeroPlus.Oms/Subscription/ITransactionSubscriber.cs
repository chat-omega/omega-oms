using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Oms.Data.Trading;

namespace ZeroPlus.Oms.Subscription
{
    public interface ITransactionSubscriber
    {
        void AddTransaction(OmsOrder order, OMSSendTransaction transaction);
        void AddTransaction(OmsOrder order, OMSExecReport execReport);
    }
}