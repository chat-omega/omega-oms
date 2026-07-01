using ExcelDna.Integration;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.AddIn.Com
{
    [ComVisible(true)]
    [ProgId(PROG_ID)]
    public class Server
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public const string PROG_ID = "ZPOMS.Com";
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();


        public void OmsDisconnectClients()
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                try
                {
                    _ = OmsCore?.UpdateManager?.StopAsync();
                    _ = OmsCore?.QuoteClient?.StopAsync();
                    _ = OmsCore?.GreekClient?.StopAsync();
                    _ = OmsCore?.EdgeScannerClient?.StopAsync();
                    _ = OmsCore?.SymbolMapClient?.StopAsync();
                    _ = OmsCore?.FullEmaClient?.StopAsync();
                    _ = OmsCore?.InterpolatorClient?.StopAsync();
                    _ = OmsCore?.TheosClient?.StopAsync();
                    _ = OmsCore?.AutoTraderClient?.StopAsync();
                    _ = OmsCore?.HerculesClientWrapper?.StopAsync();
                    _ = OmsCore?.DominatorClient?.StopAsync();
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, nameof(OmsDisconnectClients));
                }
            });
        }

        public void OmsConnectClients()
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                try
                {
                    _ = OmsCore?.UpdateManager?.RestartAsync();
                    _ = OmsCore?.QuoteClient?.RestartAsync();
                    _ = OmsCore?.GreekClient?.RestartAsync();
                    _ = OmsCore?.EdgeScannerClient?.RestartAsync();
                    _ = OmsCore?.SymbolMapClient?.RestartAsync();
                    _ = OmsCore?.FullEmaClient?.RestartAsync();
                    _ = OmsCore?.InterpolatorClient?.RestartAsync();
                    _ = OmsCore?.TheosClient?.RestartAsync();
                    _ = OmsCore?.AutoTraderClient?.RestartAsync();
                    _ = OmsCore?.HerculesClientWrapper?.RestartAsync();
                    _ = OmsCore?.DominatorClient?.RestartAsync();
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, nameof(OmsConnectClients));
                }
            });
        }

        public string OmsClientStatus()
        {
            string message = "";
            message += "AdjTheo" + (OmsCore?.UpdateManager?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Quote" + (OmsCore?.QuoteClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Hanweck" + (OmsCore?.GreekClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "EdgeScanner" + (OmsCore?.EdgeScannerClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "FullEma" + (OmsCore?.FullEmaClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Interpolator" + (OmsCore?.InterpolatorClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Theos" + (OmsCore?.TheosClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "OrderGateway" + (OmsCore?.AutoTraderClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Transaction" + (OmsCore?.HerculesClientWrapper?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "DomsManager" + (OmsCore?.DominatorClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            return message;
        }

        public void ShowMessage(string message, string title, bool silent)
        {
            try
            {
                OmsCore.DominatorClient.ShowMessage(message, title, silent);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(ShowMessage));
            }
        }

        public void RegisterDom(string workbookName, string username, string[] domSetupsArray, string[] fullAutoSetupsArray)
        {
            try
            {
                List<string> domSetups = domSetupsArray != null ? domSetupsArray.ToList() : new List<string>();
                List<string> fullAutoSetups = fullAutoSetupsArray != null ? fullAutoSetupsArray.ToList() : new List<string>();
                OmsCore.DominatorClient.RegisterDom(workbookName: workbookName,
                                                    username: username,
                                                    domSetups: domSetups,
                                                    fullAutoSetups: fullAutoSetups);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(RegisterDom));
            }
        }

        public void UpdateStatus(string status)
        {
            try
            {
                OmsCore.DominatorClient.UpdateStatus(status);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateStatus));
            }
        }

        public void UpdateDomSetup(string setup)
        {
            try
            {
                OmsCore.DominatorClient.UpdateDomSetup(setup, "");
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateDomSetup));
            }
        }

        public void UpdateDomSetupAndConfig(string setup, string config)
        {
            try
            {
                OmsCore.DominatorClient.UpdateDomSetup(setup, config);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateDomSetupAndConfig));
            }
        }

        public void UpdateDomList(string product, string type, string listDate, string listCreator, string fullName, string subName, int listCount)
        {
            try
            {
                OmsCore.DominatorClient.UpdateDomList(product: product,
                                                      type: type,
                                                      listDate: listDate,
                                                      listCreator: listCreator,
                                                      fullName: fullName,
                                                      subName: subName,
                                                      listCount: listCount);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateDomSetup));
            }
        }

        public void UpdateDomState(string state, int domCount, int fills, double edgeMultiplier, double deltaMax)
        {
            try
            {
                OmsCore.DominatorClient.UpdateDomState(state: state,
                                                       domCount: domCount,
                                                       fills: fills,
                                                       edgeMultiplier: edgeMultiplier,
                                                       deltaMax: deltaMax);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateDomState));
            }
        }

        public void UpdateUniqueSubmissionsState(bool uniqueSubmissionsOn)
        {
            try
            {
                OmsCore.DominatorClient.UpdateUniqueSubmissionsState(uniqueSubmissionsOn);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateUniqueSubmissionsState));
            }
        }

        public void UpdateLoopSize(int loopSize)
        {
            try
            {
                OmsCore.DominatorClient.UpdateLoopSize(loopSize);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateLoopSize));
            }
        }

        public void UpdateCalendarEdge(object[] edges)
        {
            try
            {
                Dictionary<int, double> dayToEdgeMap = new();
                foreach (object[] edge in edges)
                {
                    int key = Convert.ToInt32(edge[0]);
                    double value = Convert.ToDouble(edge[1]);
                    dayToEdgeMap[key] = value;
                }
                OmsCore.DominatorClient.UpdateCalendarEdge(dayToEdgeMap);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateCalendarEdge));
            }
        }

        public void UpdateDeltaEdge(object[] deltaEdges)
        {
            try
            {
                Dictionary<double, double> deltaToEdgeMap = new();
                foreach (object[] edge in deltaEdges)
                {
                    double key = Convert.ToDouble(edge[0]);
                    double value = Convert.ToDouble(edge[1]);
                    deltaToEdgeMap[key] = value;
                }
                OmsCore.DominatorClient.UpdateDeltaEdge(deltaToEdgeMap);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(UpdateDeltaEdge));
            }
        }

        public void PlaySoundById(int id)
        {
            try
            {
                OmsCore.DominatorClient.PlaySound(id);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(PlaySoundById));
            }
        }

        public void PlaySoundByName(string name)
        {
            try
            {
                OmsCore.DominatorClient.PlaySound(name);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(PlaySoundByName));
            }
        }

        public void ShowVisualNotification(int timeout)
        {
            try
            {
                OmsCore.DominatorClient.ShowVisualNotification(timeout);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(ShowVisualNotification));
            }
        }

        public void OpenIvChart(string symbol, int days, int mins, double underlyingPrice, string ivChartType)
        {
            try
            {
                if (Enum.TryParse(ivChartType, out IvChartType type))
                {
                    OmsCore.DominatorClient.OpenIvChart(symbol: symbol,
                                                        days: days,
                                                        mins: mins,
                                                        underlyingPrice: underlyingPrice,
                                                        source: Enums.UnderPriceSource.Mid,
                                                        type: type);
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenIvChart));
            }
        }

        public void OpenTicketAtLocation(string symbol, string route, double price, double underPrice, bool withContra, double left, double top)
        {
            try
            {
                OmsCore.DominatorClient.OpenTicket(symbol: symbol,
                                                   route: route,
                                                   closingRoute: string.Empty,
                                                   price: price,
                                                   underPrice: underPrice,
                                                   contraPrice: double.NaN,
                                                   contraUnderPrice: double.NaN,
                                                   edge: double.NaN,
                                                   withContra: withContra,
                                                   left: left,
                                                   top: top);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicketAtLocation));
            }
        }

        public void OpenTicketAtLocationFull(string symbol, string route, string closingRoute, double price, double underPrice, double contraFillPrice, double contraUnderPrice, double edge, bool withContra, double left, double top)
        {
            try
            {
                OmsCore.DominatorClient.OpenTicket(symbol: symbol,
                                                   route: route,
                                                   closingRoute: closingRoute,
                                                   price: price,
                                                   underPrice: underPrice,
                                                   contraPrice: contraFillPrice,
                                                   contraUnderPrice: contraUnderPrice,
                                                   edge: edge,
                                                   withContra: withContra,
                                                   left: left,
                                                   top: top);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicketAtLocationFull));
            }
        }

        public void OpenTicket(string symbol, string route, double price, double underPrice, double edge, bool withContra)
        {
            try
            {
                OmsCore.DominatorClient.OpenTicket(symbol: symbol,
                                                   route: route,
                                                   closingRoute: string.Empty,
                                                   price: price,
                                                   underPrice: underPrice,
                                                   contraPrice: double.NaN,
                                                   contraUnderPrice: double.NaN,
                                                   edge: edge,
                                                   withContra: withContra);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicket));
            }
        }

        public void OpenTicketFull(string symbol, string route, string contraRoute, double price, double underPrice, double contraFillPrice, double contraUnderPrice, double edge, bool withContra)
        {
            try
            {
                OmsCore.DominatorClient.OpenTicket(symbol: symbol,
                                                   route: route,
                                                   closingRoute: contraRoute,
                                                   price: price,
                                                   underPrice: underPrice,
                                                   contraPrice: contraFillPrice,
                                                   contraUnderPrice: contraUnderPrice,
                                                   edge: edge,
                                                   withContra: withContra);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicketFull));
            }
        }

        public void OpenTicketWithTimer(string symbol,
                                        string route,
                                        string contraRoute,
                                        double price,
                                        double underPrice,
                                        double contraFillPrice,
                                        double contraUnderPrice,
                                        int submitWithDelayInterval,
                                        bool submitWithDelayPercentBidEnabled,
                                        double submitWithDelayPercentBid,
                                        bool submitWithDelayTheoReferenceEnabled,
                                        double submitWithDelayEdgeToTheo,
                                        bool submitWithDelayDeltaAdjustEnabled,
                                        string submitWithDelaySide,
                                        double submitWithDelayDeltaAdjLevel,
                                        bool submitWithDelayBidRangeEnabled,
                                        double submitWithDelayMinBid,
                                        double submitWithDelayMaxBid,
                                        bool submitWithDelayAskRangeEnabled,
                                        double submitWithDelayMinAsk,
                                        double submitWithDelayMaxAsk,
                                        bool submitWithDelayPriceRangeEnabled,
                                        double submitWithDelayMinPrice,
                                        double submitWithDelayMaxPrice,
                                        bool submitWithDelayCancelOnUserPositionChangeEnabled,
                                        bool submitWithDelayDeltaAdjCurrentPxEnabled,
                                        double submitWithDelayDeltaAdjCurrentPxEdge,
                                        bool submitWithDelayCancelOnLegVolumeChangeEnabled,
                                        int submitWithDelayCancelOnVolumeChange,
                                        bool submitWithDelayPlayPreSubmitNotification,
                                        int submitWithDelayPreSubmitNotificationSeconds)
        {
            try
            {
                OpenTicketRequest request = new()
                {
                    TicketType = TicketType.Single,
                    Symbol = symbol,
                    Route = route,
                    ClosingRoute = contraRoute,
                    Price = price == 0 ? double.NaN : price,
                    UnderPrice = underPrice == 0 ? double.NaN : underPrice,
                    Left = -1,
                    Top = -1,
                    Hedge = false,
                    AutoHedgeEnabled = false,
                    HedgePercentage = 0,
                    ContraPrice = contraFillPrice == 0 ? double.NaN : contraFillPrice,
                    ContraUnderPrice = contraUnderPrice == 0 ? double.NaN : contraUnderPrice,
                    Edge = double.NaN,
                    Side = submitWithDelaySide,
                    SubmitWithDelaySide = submitWithDelaySide,
                    SubmitWithDelayEnabled = true,
                    SubmitWithDelayInterval = submitWithDelayInterval,
                    SubmitWithDelayPercentBidEnabled = submitWithDelayPercentBidEnabled,
                    SubmitWithDelayPercentBid = submitWithDelayPercentBid,
                    SubmitWithDelayTheoReferenceEnabled = submitWithDelayTheoReferenceEnabled,
                    SubmitWithDelayEdgeToTheo = submitWithDelayEdgeToTheo,
                    SubmitWithDelayDeltaAdjustEnabled = submitWithDelayDeltaAdjustEnabled,
                    SubmitWithDelayDeltaAdjLevel = submitWithDelayDeltaAdjLevel,
                    SubmitWithDelayBidRangeEnabled = submitWithDelayBidRangeEnabled,
                    SubmitWithDelayMinBid = submitWithDelayMinBid,
                    SubmitWithDelayMaxBid = submitWithDelayMaxBid,
                    SubmitWithDelayAskRangeEnabled = submitWithDelayAskRangeEnabled,
                    SubmitWithDelayMinAsk = submitWithDelayMinAsk,
                    SubmitWithDelayMaxAsk = submitWithDelayMaxAsk,
                    SubmitWithDelayPriceRangeEnabled = submitWithDelayPriceRangeEnabled,
                    SubmitWithDelayMinPrice = submitWithDelayMinPrice,
                    SubmitWithDelayMaxPrice = submitWithDelayMaxPrice,
                    SubmitWithDelayCancelOnUserPositionChangeEnabled = submitWithDelayCancelOnUserPositionChangeEnabled,
                    SubmitWithDelayDeltaAdjCurrentPxEnabled = submitWithDelayDeltaAdjCurrentPxEnabled,
                    SubmitWithDelayDeltaAdjCurrentPxEdge = submitWithDelayDeltaAdjCurrentPxEdge,
                    SubmitWithDelayCancelOnLegVolumeChangeEnabled = submitWithDelayCancelOnLegVolumeChangeEnabled,
                    SubmitWithDelayCancelOnVolumeChange = submitWithDelayCancelOnVolumeChange,
                    SubmitWithDelayPlayPreSubmitNotification = submitWithDelayPlayPreSubmitNotification,
                    SubmitWithDelayPreSubmitNotificationSeconds = submitWithDelayPreSubmitNotificationSeconds,
                };
                OmsCore.DominatorClient.OpenTicket(request: request);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicketWithTimer));
            }
        }

        public void OpenTicketAndHedge(string symbol, string route, double price, double underPrice, double hedgePercentage, string side)
        {
            try
            {
                OmsCore.DominatorClient.OpenTicket(symbol: symbol,
                                                   route: route,
                                                   closingRoute: string.Empty,
                                                   price: price,
                                                   underPrice: underPrice,
                                                   contraPrice: double.NaN,
                                                   contraUnderPrice: double.NaN,
                                                   edge: double.NaN,
                                                   withContra: false,
                                                   left: -1,
                                                   top: -1,
                                                   hedge: true,
                                                   autoHedgeEnabled: false,
                                                   hedgePercentage: hedgePercentage,
                                                   side: side);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicketAndHedge));
            }
        }

        public void OpenTicketAndHedgeFull(string symbol, string route, string closingRoute, double price, double underPrice, double contraFillPrice, double contraUnderPrice, double hedgePercentage, string side)
        {
            try
            {
                OmsCore.DominatorClient.OpenTicket(symbol: symbol,
                                                   route: route,
                                                   closingRoute: closingRoute,
                                                   price: price,
                                                   underPrice: underPrice,
                                                   contraPrice: contraFillPrice,
                                                   contraUnderPrice: contraUnderPrice,
                                                   edge: double.NaN,
                                                   withContra: false,
                                                   left: -1,
                                                   top: -1,
                                                   hedge: true,
                                                   autoHedgeEnabled: false,
                                                   hedgePercentage: hedgePercentage,
                                                   side: side);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenTicketAndHedgeFull));
            }
        }

        public void Open3WayTicketAtLocation(string symbol, string side, double fillPrice, double underPrice, double edge, string route, string closingRoute, string finishingRoute, double left, double top)
        {
            try
            {
                OmsCore.DominatorClient.Open3WayTicket(symbol: symbol,
                                                       side: side,
                                                       price: fillPrice,
                                                       underPrice: underPrice,
                                                       contraPrice: double.NaN,
                                                       contraUnderPrice: double.NaN,
                                                       edge: edge,
                                                       route: route,
                                                       closingRoute: closingRoute,
                                                       finishingRoute: finishingRoute,
                                                       left: left,
                                                       top: top);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(Open3WayTicketAtLocation));
            }
        }

        public void Open3WayTicketAtLocationFull(string symbol, string side, double fillPrice, double underPrice, double contraFillPrice, double contraUnderPrice, double edge, string route, string closingRoute, string finishingRoute, double left, double top)
        {
            try
            {
                OmsCore.DominatorClient.Open3WayTicket(symbol: symbol,
                                                       side: side,
                                                       price: fillPrice,
                                                       underPrice: underPrice,
                                                       contraPrice: contraFillPrice,
                                                       contraUnderPrice: contraUnderPrice,
                                                       edge: edge,
                                                       route: route,
                                                       closingRoute: closingRoute,
                                                       finishingRoute: finishingRoute,
                                                       left: left,
                                                       top: top);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(Open3WayTicketAtLocationFull));
            }
        }

        public void Open3WayTicket(string symbol, string side, double fillPrice, double underPrice, double edge, string route, string closingRoute, string finishingRoute)
        {
            try
            {
                OmsCore.DominatorClient.Open3WayTicket(symbol: symbol,
                                                       side: side,
                                                       price: fillPrice,
                                                       underPrice: underPrice,
                                                       contraPrice: double.NaN,
                                                       contraUnderPrice: double.NaN,
                                                       edge: edge,
                                                       route: route,
                                                       closingRoute: closingRoute,
                                                       finishingRoute: finishingRoute);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(Open3WayTicket));
            }
        }

        public void Open3WayTicketFull(string symbol, string side, double fillPrice, double underPrice, double contraFillPrice, double contraUnderPrice, double edge, string route, string closingRoute, string finishingRoute)
        {
            try
            {
                OmsCore.DominatorClient.Open3WayTicket(symbol: symbol,
                                                       side: side,
                                                       price: fillPrice,
                                                       underPrice: underPrice,
                                                       contraPrice: contraFillPrice,
                                                       contraUnderPrice: contraUnderPrice,
                                                       edge: edge,
                                                       route: route,
                                                       closingRoute: closingRoute,
                                                       finishingRoute: finishingRoute);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(Open3WayTicketFull));
            }
        }

        public void Open3WayFishTicket(string symbol, string side, double fillPrice, double underPrice, double edge, string route, string closingRoute, string finishingRoute, double startingEdge, double increment, double interval)
        {
            try
            {
                OmsCore.DominatorClient.Open3WayTicket(symbol: symbol,
                                                       side: side,
                                                       price: fillPrice,
                                                       underPrice: underPrice,
                                                       contraPrice: double.NaN,
                                                       contraUnderPrice: double.NaN,
                                                       edge: edge,
                                                       route: route,
                                                       closingRoute: closingRoute,
                                                       finishingRoute: finishingRoute,
                                                       left: -1,
                                                       top: -1,
                                                       startingEdge: startingEdge,
                                                       increment: increment,
                                                       interval: interval);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(Open3WayFishTicket));
            }
        }

        public void Open3WayFishTicketFull(string symbol, string side, double fillPrice, double underPrice, double contraFillPrice, double contraUnderPrice, double edge, string route, string closingRoute, string finishingRoute, double startingEdge, double increment, double interval)
        {
            try
            {
                OmsCore.DominatorClient.Open3WayTicket(symbol: symbol,
                                                       side: side,
                                                       price: fillPrice,
                                                       underPrice: underPrice,
                                                       contraPrice: contraFillPrice,
                                                       contraUnderPrice: contraUnderPrice,
                                                       edge: edge,
                                                       route: route,
                                                       closingRoute: closingRoute,
                                                       finishingRoute: finishingRoute,
                                                       left: -1,
                                                       top: -1,
                                                       startingEdge: startingEdge,
                                                       increment: increment,
                                                       interval: interval);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(Open3WayFishTicketFull));
            }
        }

        public void CloseTicket(string symbol)
        {
            try
            {
                OmsCore.DominatorClient.CloseTicket(symbol);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(CloseTicket));
            }
        }

        public void OpenBasket(string basketMode, object[] symbols)
        {
            try
            {
                OmsCore.DominatorClient.OpenBasket(mode: basketMode,
                                                   id: string.Empty,
                                                   settings: string.Empty,
                                                   symbols: symbols);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OpenBasket));
            }
        }

        public void LoadBasketWithId(string basketMode, string id, string settings, object[] symbols)
        {
            try
            {
                OmsCore.DominatorClient.OpenBasket(mode: basketMode,
                                                   id: id,
                                                   settings: settings,
                                                   symbols: symbols);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(LoadBasketWithId));
            }
        }

        public void LoadBasketAndPerm(string basketId, object[] symbols)
        {
            try
            {
                List<OpenTicketRequest> tickets = new();
                foreach (object[] symbol in symbols.Cast<object[]>())
                {
                    if (symbol != null)
                    {
                        try
                        {
                            tickets.Add(new OpenTicketRequest()
                            {
                                Symbol = Convert.ToString(symbol[0]),
                                Route = Convert.ToString(symbol[1]),
                                Side = Convert.ToString(symbol[2]),
                                Price = Convert.ToDouble(symbol[3]),
                                UnderPrice = Convert.ToDouble(symbol[4]),
                                ContraPrice = Convert.ToDouble(symbol[5]),
                                ContraUnderPrice = Convert.ToDouble(symbol[6]),
                                Edge = Convert.ToDouble(symbol[7]),
                                Qty = Convert.ToInt32(symbol[8]),
                                PermCount = Convert.ToInt32(symbol[9]),
                                PermPriceBackup = Convert.ToDouble(symbol[10]),
                            });
                        }
                        catch (Exception ex)
                        {
                            _log?.Error(ex, nameof(LoadBasketAndPerm));
                        }
                    }
                }

                OmsCore.DominatorClient.OpenPermBasket(id: basketId,
                                                       symbols: tickets);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(LoadBasketAndPerm));
            }
        }

        public void LoadBasketAndPermFull(string basketId, object[] symbols, string buyRoute, string sellRoute, double closeEdge, double closeInterval, double closeIncrement, double maxLoss, double loopInterval, double minEdge, int sizeUpQty, int loopBeforeSizeup)
        {
            try
            {
                List<OpenTicketRequest> tickets = new();
                foreach (object[] symbol in symbols.Cast<object[]>())
                {
                    if (symbol != null)
                    {
                        try
                        {
                            tickets.Add(new OpenTicketRequest()
                            {
                                Symbol = Convert.ToString(symbol[0]),
                                Route = Convert.ToString(symbol[1]),
                                Side = Convert.ToString(symbol[2]),
                                Price = Convert.ToDouble(symbol[3]),
                                UnderPrice = Convert.ToDouble(symbol[4]),
                                ContraPrice = Convert.ToDouble(symbol[5]),
                                ContraUnderPrice = Convert.ToDouble(symbol[6]),
                                Edge = Convert.ToDouble(symbol[7]),
                                Qty = Convert.ToInt32(symbol[8]),
                                PermCount = Convert.ToInt32(symbol[9]),
                                PermPriceBackup = Convert.ToDouble(symbol[10]),
                            });
                        }
                        catch (Exception ex)
                        {
                            _log?.Error(ex, nameof(LoadBasketAndPerm));
                        }
                    }
                }

                OmsCore.DominatorClient.OpenPermBasket(id: basketId,
                                                       symbols: tickets,
                                                       buyRoute: buyRoute,
                                                       sellRoute: sellRoute,
                                                       closeEdge: closeEdge,
                                                       closeInterval: closeInterval,
                                                       closeIncrement: closeIncrement,
                                                       maxLoss: maxLoss,
                                                       loopInterval: loopInterval,
                                                       minEdge: minEdge,
                                                       sizeUpQty: sizeUpQty,
                                                       loopBeforeSizeup: loopBeforeSizeup);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(LoadBasketAndPermFull));
            }
        }

        public void StartDomsBySetup(string argument)
        {
            try
            {
                OmsCore.DominatorClient.StartDoms("Setup:" + argument);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(StartDomsBySetup));
            }
        }

        public void StopDomsBySetup(string argument)
        {
            try
            {
                OmsCore.DominatorClient.StopDoms("Setup:" + argument);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(StopDomsBySetup));
            }
        }

        public void StartDoms(string argument)
        {
            try
            {
                OmsCore.DominatorClient.StartDoms(argument);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(StartDoms));
            }
        }

        public void StopDoms(string argument)
        {
            try
            {
                OmsCore.DominatorClient.StopDoms(argument);
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(StopDoms));
            }
        }

        public void SendDomEdgeToNewDom(string symbol, double domEdgeCalc, string domEdgeParamsJson)
        {
            OmsCore.DominatorClient.SendEdge(symbol, domEdgeCalc, domEdgeParamsJson);
        }

        public void SendDomFilterToNewDom(string symbol, bool domFilterResult, string domEdgeParamsJson)
        {
            OmsCore.DominatorClient.SendFilter(symbol, domFilterResult, domEdgeParamsJson);
        }

        public string SendOrderInfoToAutotrader(string symbol, double price, double contraPrice, double underBid, double underAsk, bool fishLongFirst = true)
        {
            symbol = symbol.Replace("`", "");
            var domMainOrderId = OmsCore.DominatorClient.SendAutoTraderOrder(symbol,
                side: fishLongFirst ? Side.Buy : Side.Sell,
                price: Convert.ToInt64(price * 100),
                underPrice: Convert.ToInt64(underAsk * 100),
                argsJson: string.Empty);

            _log?.Trace("AutoTrader Request Sent to OMS for {0}, at price {2}, enter long set to {1}: {2}", symbol, fishLongFirst, price, domMainOrderId);

            var domContraOrderId = OmsCore.DominatorClient.SendAutoTraderOrder(symbol,
                side: fishLongFirst ? Side.Sell : Side.Buy,
                price: Convert.ToInt64(contraPrice * 100),
                underPrice: Convert.ToInt64(underBid * 100),
                cancelSendParent: domMainOrderId,
                argsJson: string.Empty);

            _log?.Trace("AutoTrader Request Sent to OMS for {0}, to attempt the other side at {1}: {2}", symbol, contraPrice, domContraOrderId);

            return @$"{domMainOrderId}|{domContraOrderId}";
        }

        public string SendOrderSideToAutotrader(string mainId, string symbol, int side, double price, double underMid)
        {
            symbol = symbol.Replace("`", "");
            Side sideEnum = side switch
            {
                2 => Side.Sell,
                1 => Side.Buy,
                _ => throw new ArgumentException($"Invalid Side Code {side}"),
            };
            Guid? mainOrderGuid = string.IsNullOrWhiteSpace(mainId) ? null : new Guid(mainId);

            var domOrderId = OmsCore.DominatorClient.SendAutoTraderOrder(symbol,
                side: sideEnum,
                price: Convert.ToInt64(price * 100),
                underPrice: Convert.ToInt64(underMid * 100),
                cancelSendParent: mainOrderGuid,
                argsJson: string.Empty);

            _log?.Trace("AutoTrader Request Sent to OMS for {0}, at price {2}, enter {1}: {3}", symbol, sideEnum, price, mainOrderGuid);

            return domOrderId.ToString();
        }

        public void SendDomCommand(string commandCode, string argument)
        {
            OmsCore.DominatorClient.SendCommand(commandCode, argument);
        }
    }
}
