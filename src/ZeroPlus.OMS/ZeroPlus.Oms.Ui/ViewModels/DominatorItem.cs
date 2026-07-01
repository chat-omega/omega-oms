using NLog;
using System;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class DominatorItem : OrderTicket
    {
        public DominatorItem(DominatorTraderModel dominatorTraderModel, DominatorConfig dominatorConfig, OmsCore omsCore)
            : base(dominatorTraderModel._ticketFactory,
                   dominatorTraderModel._threeWayCloserFactory,
                   dominatorTraderModel._routeSelectionViewFactory,
                   dominatorTraderModel._transactionConsumer,
                   dominatorTraderModel._notificationManager,
                   dominatorTraderModel._portfolioManagerModel,
                   omsCore)
        {
            this.dominatorConfig = dominatorConfig;
        }

        private readonly DominatorConfig dominatorConfig;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public bool FilterApproved { get; set; } = true;
        public bool FishingStandby { get; set; } = false;

        private void DTEFilter()
        {
            var approved = Legs.Select(leg =>
                Math.Floor(leg.ExpirationInfo.Expiration.Subtract(DateTime.Now).TotalDays))
            .All(expirationDate => dominatorConfig.MinDTE <= expirationDate && expirationDate <= dominatorConfig.MaxDTE);
            FilterApproved &= approved;
        }

        OmsOrder LastDomOrder { get; set; }

        private void EmaWidthFilter()
        {

        }

        private void EmaBidAskFilter()
        {

        }

        private void NegativeEdgeFilter()
        {
            var denied = Edge < 0;
            FilterApproved &= !denied;
        }

        private void DOMUnfishedFilter()
        {

        }

        private void ZeroBidFilter()
        {
            var denied = Bid == 0;
            FilterApproved &= !denied;
        }

        private void MaxSpreadDelta()
        {
            var denied = TotalDelta > dominatorConfig.MaxSpreadDelta;
            FilterApproved &= !denied;
        }

        private void ZpMaxUnfilledFilter()
        {

        }

        private void LooserThresholdFilter()
        {

        }

        private void DayTradeFilter()
        {
            var approved = dominatorConfig.AllowIfDayTraded;
            FilterApproved &= approved;
        }

        private void ApplyDeltaEdgeExpansion()
        {
            Edge *= TotalDelta * dominatorConfig.EdgeExpansionPerDelta;
        }

        private void UnderlyingEdgeExpansion()
        {
            var m = dominatorConfig.UnderlyingMultiplier.FirstOrDefault(x => x.Symbol == Underlying)?.Multiplier ?? 1.0;
            Edge *= UnderEma * m;
        }

        private void MinEdgeByDTETier()
        {
            var minEdge = dominatorConfig.DTEMultiplier.FirstOrDefault(x => DaysToExpiration < x.Dte).Multiplier;
            Edge = Math.Max(Edge, minEdge);
        }
        public bool CalculateFilter()
        {
            DayTradeFilter();
            MinEdgeByDTETier();
            LooserThresholdFilter();
            ZeroBidFilter();
            ZpMaxUnfilledFilter();
            MaxSpreadDelta();
            DOMUnfishedFilter();
            NegativeEdgeFilter();
            EmaBidAskFilter();
            EmaWidthFilter();
            DTEFilter();
            return FilterApproved;
        }
        public double CalculateEdge()
        {
            UnderlyingEdgeExpansion();
            ApplyDeltaEdgeExpansion();
            MinEdgeByDTETier();
            return Edge;
        }
        public bool ExcelFilterReady = false;
        public bool ExcelEdgeReady = false;
        public async Task EdgeAndFilterCalculationComplete()
        {
            int i = 100;
            while (i-- > 0)
            {
                await Task.Delay(100);
                if (ExcelEdgeReady && ExcelFilterReady) return;
            }
        }
        public async Task SendOrderAndRegisterFillEvents()
        {
            await SubmitOrder();
            //SubmitFishOrder(nameof(DominatorTraderModel));
        }


        internal void RegisterEvents(DominatorTraderModel dominatorTraderModel)
        {
            base.RegisterEvents(dominatorTraderModel);
        }

        internal void SubscribeToData(string source)
        {
            if (source?.ToUpper() == "UNDERLYING")
            {
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Ask, this);
                return;
            }
            foreach (TicketLegModel leg in Legs) leg.SubscribeToDataFeed(source);
        }

        internal void UnsubscribeData(string source)
        {
            if (source?.ToUpper() == "UNDERLYING")
            {
                OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.Ask, this);
                Last = double.NaN;
                UnderMid = double.NaN;
                UnderBid = double.NaN;
                UnderAsk = double.NaN;
                return;
            }
            foreach (TicketLegModel leg in Legs) leg.UnsubscribeFromDataSource(source);
        }

        protected override Task ProcessAutomation(OrderUpdateModel execReport, DateTime receiveTime, OrderUpdateValues orderUpdateValues,
            bool isMainOrder, bool isContraOrder)
        {
            return Task.CompletedTask;
        }
    }
}
