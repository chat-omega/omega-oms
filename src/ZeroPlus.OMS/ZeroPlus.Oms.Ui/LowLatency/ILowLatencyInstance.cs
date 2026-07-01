using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.LowLatency
{
    public interface ILowLatencyInstance
    {
        public event LowLatencyStateChangedHandler LowLatencyStateChanged;

        void Init(ILowLatencyModel lowLatencyModel);

        Task LoadFromFile(string path);
        Task<bool> ConnectAsync(bool testMode);

        void Start();
        void Stop(bool killAll);

        void UploadAllChanges();
        void UploadInitiatorChanges();
        void UploadLiquidatorChanges();
        void UploadSignalChanges();
        void UploadRiskChanges();
        void UploadManualAdjustment(LowLatencyOrderModel orderModel, IOmsOrder trade);
        void RequestManualAdj(string orderModelUserName,
            string orderModelSymbol,
            double tradeAveragePrice,
            int tradeCumulativeQuantity,
            Side orderModelSide,
            string orderModelClOrdId,
            string orderModelWho,
            bool doNothing,
            bool bypassRisk);
    }
}
