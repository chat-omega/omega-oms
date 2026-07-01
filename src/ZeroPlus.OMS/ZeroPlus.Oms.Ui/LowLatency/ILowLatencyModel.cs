using System.Collections.Generic;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.LowLatency
{
    public interface ILowLatencyModel
    {
        string Name { get; set; }
        string Username { get; set; }
        int InstanceId { get; set; }
        int Rank { get; set; }
        string Host { get; set; }
        int SymbolsCount { get; set; }
        HashSet<string> Symbols { get; set; }
        InitiatorModel Initiator { get; set; }
        LoopModel Loop { get; set; }
        LiquidatorModel Liquidator { get; set; }
        SignalModel Signal { get; set; }
        LowLatencyRiskModel Risk { get; set; }
        string AppProcessThread { get; set; }
        string LiveStrategies { get; set; }
        bool ForceResendWatchlist { get; set; }
        string Message { get; set; }
        void SetUsername();
        void SetMessage(string error);
    }
}