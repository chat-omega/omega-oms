using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.ViewModels;

public class GammaScalpOrderTicket : OrderTicket
{
    protected static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    public GammaScalpOrderTicket(
        IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
        IAbstractFactory<ThreeWayCloser> threeWayCloserFactory,
        IAbstractFactory<RouteSelectionViewModel> routeSelectionViewFactory,
        TransactionConsumerModel transactionConsumer,
        NotificationManager notificationManager,
        PortfolioManagerModel portfolioManagerModel,
        OmsCore omsCore) : base(ticketFactory, threeWayCloserFactory, routeSelectionViewFactory, transactionConsumer, notificationManager, portfolioManagerModel, omsCore)
    {
        TicketStyle = OrderTicketStyle.GammaScalp;
    }

    [Command]
    public void CustomSummary(RowSummaryArgs args)
    {
        if (!args.IsTotalSummary || args.SummaryProcess != SummaryProcess.Finalize)
        {
            return;
        }

        bool use3DecimalPlacesForGreeks = false;
        if (OmsCore != null)
        {
            use3DecimalPlacesForGreeks = OmsCore.Config.DecimalPlacesForGreeks == 3;
        }

        switch (args.SummaryItem.PropertyName)
        {
            case "Quantity":
                args.TotalValue = Legs.Count > 0 ? $"Qty:{Lcd:n0}" : "N/A";
                break;
            case "Delta":
                if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                {
                    args.TotalValue = $"Delta:{TotalDelta * 100:N0}";
                }
                else
                {
                    args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalDelta:#,###.###}" : $"{TotalDelta:#,###.##}";
                }
                break;
            case "Gamma":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalGamma:#,###.###}" : $"{TotalGamma:#,###.##}";
                break;
            case "Theta":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalTheta:#,###.###}" : $"{TotalTheta:#,###.##}";
                break;
            case "NetGamma":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{NetGamma:#,###.###}" : $"{NetGamma:#,###.##}";
                break;
            case "NetTheta":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{NetTheta:#,###.###}" : $"{NetTheta:#,###.##}";
                break;
            case "Vega":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalVega:#,###.###}" : $"{TotalVega:#,###.##}";
                break;
            case "Rho":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalRho:#,###.###}" : $"{TotalRho:#,###.##}";
                break;
            case "Implied":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalImplied:#,###.###}" : $"{TotalImplied:#,###.##}";
                break;
            case "Theo":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalTheo:#,###.###}" : $"{TotalTheo:#,###.##}";
                break;
            case "DeltaAdjTheo":
                args.TotalValue = use3DecimalPlacesForGreeks ? $"{TotalDeltaAdjTheo:#,###.###}" : $"{TotalDeltaAdjTheo:#,###.##}";
                break;
            case "HighestBid":
                args.TotalValue = $"{HighestBid:#,###.##}";
                break;
            case "LowestAsk":
                args.TotalValue = $"{LowestAsk:#,###.##}";
                break;
        }
    }

    protected override Task<bool> ProcessAutomation(OrderUpdateModel execReport, DateTime receiveTime, OrderUpdateValues orderUpdateValues, bool isMainOrder, bool isContraOrder)
    {
        if (orderUpdateValues.OrderStatus == OrderStatus.Canceled)
        {
            if (IsGammaScalpTicket && GammaScalpOrderResubmitOnCancel)
            {
                if (isMainOrder)
                {
                    if (PartiallyFilled)
                    {
                        UpdateQty(CumulativeQty);
                        CumulativeQty = 0;
                        LeavesQty = 0;
                        PartiallyFilled = false;
                    }
                    _ = SubmitAsync();
                }
                else if (isContraOrder)
                {
                    if (ContraPartiallyFilled)
                    {
                        UpdateQty(ContraCumulativeQty);
                        ContraCumulativeQty = 0;
                        ContraLeavesQty = 0;
                        ContraPartiallyFilled = false;
                    }
                    _ = SubmitContraAsync();
                }
            }
        }
        else if (execReport.ExecutionType == ExecutionType.PartiallyFilled || execReport.ExecutionType == ExecutionType.Trade)
        {
            int filledQty = Math.Abs(execReport.LastQty);
            int cumulativeQty = Math.Abs(execReport.CumQty);
            int leavesQty = Math.Abs(execReport.LeavesQty);
            if (isMainOrder)
            {
                LastFillPx = orderUpdateValues.AveragePrice;
                LastFillUnderBidPx = UnderBid;
                LastFillUnderPx = UnderMid;
                LastFillUnderAskPx = UnderAsk;
                LastFillAdjTheo = NetDeltaAdjTheo;
                PartiallyFilled = true;
                CumulativeQty += filledQty;
                LeavesQty = leavesQty;
                double fillPercent = (double)CumulativeQty / Lcd;
                _log.Info("Ticket partial fill received. [Open] " +
                          "Loop enabled: " + SpeedTraderClosingType + ", " +
                          "Spread ID: " + SpreadId + ", " +
                          "Last fill px: " + LastFillPx + ", " +
                          "Last fill qty: " + filledQty + ", " +
                          "Last order cumulative qty: " + cumulativeQty + ", " +
                          "Leaves qty: " + leavesQty + ", " +
                          "Total cumulative: " + CumulativeQty + ", " +
                          "Filled percent: " + fillPercent + ".");
            }
            else if (isContraOrder)
            {
                LastContraFillPx = orderUpdateValues.AveragePrice;
                LastFillUnderBidPx = UnderBid;
                LastFillUnderPx = UnderMid;
                LastFillUnderAskPx = UnderAsk;
                LastContraFillAdjTheo = NetDeltaAdjTheo;
                ContraPartiallyFilled = true;
                ContraCumulativeQty += filledQty;
                ContraLeavesQty = leavesQty;
                double fillPercent = (double)ContraCumulativeQty / Lcd;
                _log.Info("Ticket partial fill received. [Close] " +
                          "Loop enabled: " + SpeedTraderClosingType + ", " +
                          "Spread ID: " + SpreadId + ", " +
                          "Last fill px: " + LastContraFillPx + ", " +
                          "Last fill qty: " + filledQty + ", " +
                          "Last order cumulative qty: " + cumulativeQty + ", " +
                          "Leaves qty: " + leavesQty + ", " +
                          "Total cumulative: " + ContraCumulativeQty + ", " +
                          "Filled percent: " + fillPercent + ".");
            }

            if (OmsCore.Config.TicketCancelOnPartialFill)
            {
                RequestCancel(execReport);
            }
        }

        return Task.FromResult(false);
    }
}