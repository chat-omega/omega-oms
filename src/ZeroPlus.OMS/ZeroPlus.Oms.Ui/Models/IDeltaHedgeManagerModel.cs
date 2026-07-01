using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public interface IDeltaHedgeManagerModel
    {
        bool IsHedging { get; }
        OrderType OrderType { get; }
        LimitHandling LimitHandling { get; }
        string Account { get; }
        bool GammaScalper { get; }
        bool RoundDeltaForHedge { get; set; }
        double AutoHedgeLimitDiff { get; set; }
        double InitialHedgeLimitDiff { get; set; }
    }
}