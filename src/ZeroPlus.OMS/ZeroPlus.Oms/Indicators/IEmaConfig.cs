using ZeroPlus.Oms.Enums;
namespace ZeroPlus.Oms.Indicators
{
    public delegate void ResetEmaEventHandler();
    public interface IEmaConfig
    {
        event ResetEmaEventHandler ResetEmaEvent;
        bool EmaEnabled { get; set; }
        EmaType SelectedEmaType { get; set; }
        double PercentVegaThreshold { get; set; }
        double EmaSmoothing { get; set; }
        double EmaPeriods { get; set; }
        double EmaInterval { get; set; }
        double MaxBidDeviation { get; set; }
        double MaxAskDeviation { get; set; }
    }
}