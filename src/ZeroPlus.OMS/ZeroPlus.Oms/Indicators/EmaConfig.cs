using ZeroPlus.Oms.Enums;
namespace ZeroPlus.Oms.Indicators
{
    public class EmaConfig : IEmaConfig
    {
        public event ResetEmaEventHandler ResetEmaEvent;
        public bool EmaEnabled { get; set; } = true;
        public EmaType SelectedEmaType { get; set; } = EmaType.Off;
        public double PercentVegaThreshold { get; set; } = .1;
        public double EmaSmoothing { get; set; } = 2.0;
        public double EmaInterval { get; set; } = 5000;
        public double EmaPeriods { get; set; } = 20;
        public double MaxBidDeviation { get; set; } = .03;
        public double MaxAskDeviation { get; set; } = .03;

        public void ResetEma()
        {
            ResetEmaEvent?.Invoke();
        }
    }
}
