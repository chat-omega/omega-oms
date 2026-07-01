using System;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.Oms.Clients;

public interface IOrderInfoUpdateHandler
{
    void OrderInfoUpdated(OrderInfoUpdate update);
    void OrderUpdated(OrderUpdateValues orderUpdate);
    void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime);
    void AutomationStateChanged(bool running);
}