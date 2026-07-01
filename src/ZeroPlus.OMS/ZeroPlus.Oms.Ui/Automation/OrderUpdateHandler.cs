using DevExpress.Mvvm;
using System;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Ui.Automation;

public abstract class OrderUpdateHandler : BindableBase, IOmsOrderUpdateSubscriber
{
    public abstract OrderSubType? SubType { get; set; }

    public abstract void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime);
    
    public virtual void OrderInfoUpdated(OrderInfoUpdate update)
    { }

    public virtual void OrderUpdated(OrderUpdateValues orderUpdate)
    { }

    
    public virtual void AutomationStateChanged(bool running)
    { }

    public virtual void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
    { }
}