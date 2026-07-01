using System;
using System.Threading.Tasks;

namespace ZeroPlus.Oms.Managers
{
    public interface IBasket
    {
        bool IsEdgeScanFeedAutoTrader { get; set; }
        string ModuleTitle { get; set; }
        string Username { get; set; }
        string Host { get; set; }
        string Setup { get; set; }
        string List { get; set; }
        int RowCount { get; set; }
        int Fills { get; set; }
        string Uid { get; set; }
        string InstanceId { get; set; }
        TimeSpan ResubmitCountDown { get; set; }
        int ResubmitIntervalSec { get; set; }
        bool ResubmitOnTimer { get; set; }
        string SampleDescription { get; set; }
        string Tag { get; set; }

        void ReverseSidesNoCheck();
        void FlipCpNoCheck();
        void OppCpNoCheck();
        Task CleanInvalidRows(bool withUndoPrompt);
        void ClearQty();
        Task SubmitAllNoCheckSafe();
        Task ModifyAllNoCheck();
        void CancelAllNoCheck();
        void ResetTimerNoCheck();
        void StopAllLoops();
        string GetEdgeType();
        double GetEdge();
        void SetEdge(string edgeType, double edge);
        void EnableResubmitTimer(int interval);
        void DisableResubmitTimer(int interval);
        void EnableOpenTicket();
        void DisableOpenTicket();
        void EnableTicketProxy();
        void DisableTicketProxy();
        bool GetOpenTicketState();
        void Activate();
        void Hide();
        void Close();
    }
}
