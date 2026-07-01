using ExcelDna.Integration;
using NLog;
using Python.Runtime;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Utils.Spreads;
using ZeroPlus.Oms.AddIn.Rtd;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Indicators;

namespace ZeroPlus.Oms.AddIn.Functions
{
    public class Functions
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private const int MAX_REQUEST_DAYS = 15;

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public static EmaConfig EmaConfig => ServiceLocator.GetService<EmaConfig>();

        [ExcelFunction("Oms Disconnect Clients")]
        public static object OmsDisconnectClients()
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                try
                {
                    _ = OmsCore?.UpdateManager?.StopAsync();
                    _ = OmsCore?.QuoteClient?.StopAsync();
                    _ = OmsCore?.GreekClient?.StopAsync();
                    _ = OmsCore?.EdgeScannerClient?.StopAsync();
                    _ = OmsCore?.HerculesClientWrapper?.StopAsync();
                    _ = OmsCore?.FullEmaClient?.StopAsync();
                    _ = OmsCore?.InterpolatorClient?.StopAsync();
                    _ = OmsCore?.TheosClient?.StopAsync();
                    _ = OmsCore?.AutoTraderClient?.StopAsync();
                    _ = OmsCore?.DominatorClient?.StopAsync();
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, nameof(OmsDisconnectClients));
                }
            });
            return "Stopped";
        }

        [ExcelFunction("Oms Disconnect Clients")]
        public static object OmsConnectClients()
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                try
                {
                    _ = OmsCore?.UpdateManager?.RestartAsync();
                    _ = OmsCore?.QuoteClient?.RestartAsync();
                    _ = OmsCore?.GreekClient?.RestartAsync();
                    _ = OmsCore?.FullEmaClient?.RestartAsync();
                    _ = OmsCore?.TheosClient?.RestartAsync();
                    _ = OmsCore?.AutoTraderClient?.RestartAsync();
                    _ = OmsCore?.DominatorClient?.RestartAsync();
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, nameof(OmsConnectClients));
                }
            });
            return "Starting Clients";
        }

        [ExcelFunction("Oms Client Status")]
        public static object OmsClientStatus()
        {
            string message = "";
            message += "AdjTheo" + (OmsCore?.UpdateManager?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Quote" + (OmsCore?.QuoteClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Raptor" + (OmsCore?.GreekClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "EdgeScanner" + (OmsCore?.EdgeScannerClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Transaction" + (OmsCore?.HerculesClientWrapper?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "DomsManager" + (OmsCore?.DominatorClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Interpolator" + (OmsCore?.InterpolatorClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "Theos" + (OmsCore?.TheosClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "FullEma" + (OmsCore?.FullEmaClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            message += "OrderGateway" + (OmsCore?.AutoTraderClient?.IsConnected ?? false ? ":Connected;" : ":Disconnected;");
            return message;
        }

        [ExcelFunction("Retrieves a quote from ZeroPlus OMS RTD Server")]
        public static object OmsSubscribeQuote(
            [ExcelArgument(Description = "Ticker symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "Field of the ticker to get a quote on", Name = "Field")] string field,
            [ExcelArgument(Description = "Instance to pull Vola data from (OPTIONAL)", Name = "Model")] string model = "V0")
        {
            symbol = symbol.Replace("`", "");
            return XlCall.RTD(Rtd.OmsAddinRtdServer.PROG_ID, null, symbol, field.Replace("_", "").Replace(" ", ""), model);
        }

        [ExcelFunction("Updates Ema Config")]
        public static object OmsUpdateEmaConfig(
            [ExcelArgument(Description = "Ema Enabled", Name = "EmaEnabled")] bool emaEnabled,
            [ExcelArgument(Description = "Ema Smoothing", Name = "EmaSmoothing")] double emaSmoothing,
            [ExcelArgument(Description = "Ema Interval", Name = "EmaInterval")] double emaInterval,
            [ExcelArgument(Description = "Ema Periods", Name = "EmaPeriods")] double emaPeriods,
            [ExcelArgument(Description = "Percent Vega Threshold", Name = "PercentVegaThreshold")] double percentVegaThreshold)
        {
            EmaConfig.EmaEnabled = emaEnabled;
            EmaConfig.EmaSmoothing = emaSmoothing;
            EmaConfig.EmaInterval = emaInterval;
            EmaConfig.EmaPeriods = emaPeriods;
            EmaConfig.PercentVegaThreshold = percentVegaThreshold;

            return $"Ema Enabled: {EmaConfig.EmaEnabled}, " +
                   $"Ema Smoothing: {EmaConfig.EmaSmoothing}, " +
                   $"Ema Periods: {EmaConfig.EmaPeriods}, " +
                   $"Ema Interval: {EmaConfig.EmaInterval}, " +
                   $"Percent Vega Threshold: {EmaConfig.PercentVegaThreshold}.";
        }

        [ExcelFunction("OMS calculate IV")]
        public static object OmsCalculateIv(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "Underlying Price", Name = "UnderlyingPrice")] double underPrice,
            [ExcelArgument(Description = "Option Price", Name = "OptionPrice")] double optionPrice)
        {
            if (string.IsNullOrEmpty(symbol) || (!symbol.StartsWith('.') && !symbol.StartsWith('+') && !symbol.StartsWith('-')))
            {
                return "Invalid symbol";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsCalculateIv), symbol,
                   delegate
                   {
                       return OmsCalculateIvAsync(symbol, underPrice, optionPrice, DateTime.Now);
                   });
        }

        [ExcelFunction("OMS calculate option price")]
        public static object OmsCalculateIvPrice(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "Underlying Price", Name = "UnderlyingPrice")] double underPrice,
            [ExcelArgument(Description = "Option Price", Name = "OptionPrice")] double optionPrice)
        {
            if (string.IsNullOrEmpty(symbol) || (!symbol.StartsWith('.') && !symbol.StartsWith('+') && !symbol.StartsWith('-')))
            {
                return "Invalid symbol";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsCalculateIvPrice), symbol,
                   delegate
                   {
                       return OmsCalculateIvPriceAsync(symbol, underPrice, optionPrice, DateTime.Now);
                   });
        }

        [ExcelFunction("OMS calculate option price")]
        public static object OmsCalculateOptionPrice(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "IV", Name = "IV")] double iv,
            [ExcelArgument(Description = "Underlying Price", Name = "UnderlyingPrice")] double underPrice)
        {
            if (string.IsNullOrEmpty(symbol) || (!symbol.StartsWith('.') && !symbol.StartsWith('+') && !symbol.StartsWith('-')))
            {
                return "Invalid symbol";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsCalculateOptionPrice), symbol,
                   delegate
                   {
                       return OmsCalculateOptionPriceAsync(symbol, iv, underPrice, DateTime.Now);
                   });
        }

        [ExcelFunction("Oms Request Best Edge To Theo")]
        public static object OmsRequestBestEdgeToTheo(
            [ExcelArgument(Description = "Symbol", Name = "Symbol")] string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return "Invalid symbol";
            }

            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestBestEdgeToTheo), symbol,
                   delegate
                   {
                       return OmsRequestBestEdgeToTheoAsync(symbol);
                   });
        }

        [ExcelFunction("Retrieves fish status for a given symbol")]
        public static object OmsRequestSymbolFishStatus(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol)
        {
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestSymbolFishStatus), symbol,
                   delegate
                   {
                       return OmsRequestSymbolFishStatusAsync(symbol);
                   });
        }

        [ExcelFunction("Retrieves historical implied volatility chain for a given symbol")]
        public static object OmsRequestIvChain(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestIvChain), symbol,
                   delegate
                   {
                       return OmsRequestIvChainAsync(symbol, days);
                   });
        }

        [ExcelFunction("Retrieves historical implied volatility chain for a given symbol")]
        public static object OmsRequestRecalculatedIvChain(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestRecalculatedIvChain), symbol,
                   delegate
                   {
                       return OmsRequestRecalculatedIvChainAsync(symbol, days);
                   });
        }

        [ExcelFunction("Retrieves historical implied volatility chain for a given symbol")]
        public static object OmsRequestRecalculatedIvChainUsingPrice(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days,
            [ExcelArgument(Description = "Price to use for calculation", Name = "Price")] double price)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestRecalculatedIvChainUsingPrice), symbol,
                   delegate
                   {
                       return OmsRequestRecalculatedIvChainAsync(symbol, days, price);
                   });
        }

        [ExcelFunction("Retrieves tightest implied volatility chain in price terms for a given symbol")]
        public static object OmsRequestTightestEdgeToTheo(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestTightestEdgeToTheo), symbol,
                   delegate
                   {
                       return OmsRequestTightestEdgeToTheoAsync(symbol, days);
                   });
        }

        [ExcelFunction("Retrieves edge to theo using Lucas model")]
        public static object OmsRequestEdgeToTheoUsingLucasModel(
            [ExcelArgument(Description = "Underlying Symbol", Name = "Symbol")] string underlying,
            [ExcelArgument(Description = "Delta 1", Name = "Delta1")] double delta1,
            [ExcelArgument(Description = "Delta 2", Name = "Delta2")] double delta2,
            [ExcelArgument(Description = "Side", Name = "Side")] int side,
            [ExcelArgument(Description = "CallPut", Name = "CallPut")] int callPut,
            [ExcelArgument(Description = "Width", Name = "Width")] double width,
            [ExcelArgument(Description = "DTE1", Name = "DTE1")] double daysToExpiration1,
            [ExcelArgument(Description = "DTE2", Name = "DTE1")] double daysToExpiration2)
        {
            return ExcelAsyncUtil.Run(nameof(OmsRequestEdgeToTheoUsingLucasModel), null,
            delegate
            {
                return PythonCall(underlying, delta1, delta2, side, callPut, width, daysToExpiration1, daysToExpiration2);
            });
        }

        private static object PythonCall(string underlying, double delta1, double delta2, int side, int callPut, double width, double dte1, double dte2)
        {
            try
            {
                using (Py.GIL())
                {
                    using PyModule scope = Py.CreateScope();
                    string scriptPath = Path.Combine(@"\\192.168.60.12", "zeroplusshared", "EdgeToTheoModels", "dom_interface.py");
                    string code = File.ReadAllText(scriptPath);
                    dynamic compiled = PythonEngine.Compile(code);
                    scope.Execute(compiled);
                    dynamic func = scope.Get("edge_to_theo");
                    dynamic edgeToTheo = func(delta1, delta2, side, width, callPut, dte1, dte2, underlying);
                    return Convert.ToDouble(edgeToTheo);
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [ExcelFunction("Retrieves tightest implied volatility chain in price terms for a given symbol")]
        public static object OmsRequestTightestEdgeToTheoUsingPrice(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days,
            [ExcelArgument(Description = "Price to use for calculation", Name = "Price")] double price)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestTightestEdgeToTheo), symbol,
                   delegate
                   {
                       return OmsRequestTightestEdgeToTheoAsync(symbol, days, price);
                   });
        }

        [ExcelFunction("Retrieves historical implied volatility chain in price terms for a given symbol")]
        public static object OmsRequestRecalculatedIvPriceChain(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestRecalculatedIvPriceChain), symbol,
                   delegate
                   {
                       return OmsRequestRecalculatedIvPriceChainAsync(symbol, days);
                   });
        }

        [ExcelFunction("Retrieves historical implied volatility chain in price terms for a given symbol")]
        public static object OmsRequestRecalculatedIvPriceChainUsingPrice(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbol,
            [ExcelArgument(Description = "TimeSpan in days", Name = "Days")] double days,
            [ExcelArgument(Description = "Price to use for calculation", Name = "Price")] double price)
        {
            if (days < 0)
            {
                return "Invalid days";
            }
            symbol = symbol.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestRecalculatedIvPriceChainUsingPrice), symbol,
                   delegate
                   {
                       return OmsRequestRecalculatedIvPriceChainAsync(symbol, days, price);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestMatchingHanweckUpdates(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestMatchingHanweckUpdates), symbols,
                   delegate
                   {
                       return RequestMatchingHanweckUpdatesAsync(symbols);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestAdjustedEdgeSummary(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols,
            [ExcelArgument(Description = "Mins", Name = "Mins")] double mins,
            [ExcelArgument(Description = "Under Price", Name = "Price")] double price,
            [ExcelArgument(Description = "Percentage of under from given under", Name = "PercentFromUnder")] double percentFromUnder)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestAdjustedEdgeSummary), symbols,
                   delegate
                   {
                       return OmsRequestAdjustedEdgeSummaryAsync(symbols, mins, price, percentFromUnder);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestAdjustedEdgeSummaryDetails(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols,
            [ExcelArgument(Description = "Mins", Name = "Mins")] double mins,
            [ExcelArgument(Description = "Under Price", Name = "Price")] double price,
            [ExcelArgument(Description = "Percentage of under from given under", Name = "PercentFromUnder")] double percentFromUnder)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestAdjustedEdgeSummaryDetails), symbols,
                   delegate
                   {
                       return OmsRequestAdjustedEdgeSummaryAsync(symbols, mins, price, percentFromUnder, withDetails: true);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestEdgeSummaryNew(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols,
            [ExcelArgument(Description = "Mins", Name = "Mins")] double mins,
            [ExcelArgument(Description = "Under Price", Name = "Price")] double price,
            [ExcelArgument(Description = "Percentage of under from given under", Name = "PercentFromUnder")] double percentFromUnder)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestEdgeSummaryNew), symbols,
                   delegate
                   {
                       return OmsRequestEdgeSummaryMemAsync(symbols, mins, price, percentFromUnder);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestEdgeSummaryRaw(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols,
            [ExcelArgument(Description = "Mins", Name = "Mins")] double mins,
            [ExcelArgument(Description = "Under Price", Name = "Price")] double price,
            [ExcelArgument(Description = "Percentage of under from given under", Name = "PercentFromUnder")] double percentFromUnder)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestEdgeSummaryRaw), symbols,
                   delegate
                   {
                       return OmsRequestEdgeSummaryRawAsync(symbols, mins, price, percentFromUnder);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestEdgeSummary(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols,
            [ExcelArgument(Description = "Days", Name = "Days")] double days,
            [ExcelArgument(Description = "Mins", Name = "Mins")] double mins,
            [ExcelArgument(Description = "Under Price", Name = "Price")] double price,
            [ExcelArgument(Description = "Percentage of under from given under", Name = "PercentFromUnder")] double percentFromUnder,
            [ExcelArgument(Description = "Skip first and last 2 mins of the day", Name = "SkipOpenClose")] bool skipOpenClose)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestEdgeSummary), symbols,
                   delegate
                   {
                       return OmsRequestEdgeSummaryAsync(symbols, days, mins, price, percentFromUnder, skipOpenClose, groupByDay: false);
                   });
        }

        [ExcelFunction("Retrieves matching hw theo updates for given options")]
        public static object OmsRequestDailyEdgeSummary(
            [ExcelArgument(Description = "Option Symbol", Name = "Symbol")] string symbols,
            [ExcelArgument(Description = "Days", Name = "Days")] double days,
            [ExcelArgument(Description = "Mins", Name = "Mins")] double mins,
            [ExcelArgument(Description = "Under Price", Name = "Price")] double price,
            [ExcelArgument(Description = "Percentage of under from given under", Name = "PercentFromUnder")] double percentFromUnder,
            [ExcelArgument(Description = "Skip first and last 2 mins of the day", Name = "SkipOpenClose")] bool skipOpenClose)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return "Invalid symbol";
            }
            symbols = symbols.Replace("`", "");

            return ExcelAsyncUtil.Run(nameof(OmsRequestDailyEdgeSummary), symbols,
                   delegate
                   {
                       return OmsRequestEdgeSummaryAsync(symbols, days, mins, price, percentFromUnder, skipOpenClose, groupByDay: true);
                   });
        }

        [ExcelFunction("Oms Request Market Cross Scan")]
        public static object OmsRequestMarketCrossScan(
            [ExcelArgument(Description = "Lookback Seconds", Name = "LookbackSeconds")] double lookbackSeconds,
            [ExcelArgument(Description = "Min Market Cross", Name = "MinMarketCross")] double minMarketCross,
            [ExcelArgument(Description = "Current Market Width", Name = "CurrentMarketWidth")] double currentMarketWidth)
        {
            return ExcelAsyncUtil.Run(nameof(OmsRequestMarketCrossScan), null,
                   delegate
                   {
                       return RequestMarketCrossScan(lookbackSeconds, minMarketCross, currentMarketWidth);
                   });
        }

        [ExcelFunction("Oms Request Fee Calculation")]
        public static object OmsRequestFeeCalculation(string symbol, string exchange)
        {
            double totalFees = 0.0;
            try
            {
                double rebate = 0;
                SymbolCodec legs = new(symbol);
                bool isSingleLeg = legs.LegCount == 1;
                string underlying = legs.UnderlyingSymbol();
                if (underlying != null && OmsCore.Config.UnderlyingToCommissionsMap.TryGetValue(underlying, out Comms.Models.Data.Oms.Commission commissions))
                {
                    if (string.IsNullOrWhiteSpace(exchange))
                    {
                        rebate = isSingleLeg
                            ? 0.30
                            : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Average();
                    }
                    else if (!commissions.Lookup.TryGetValue(exchange.ToUpper(), out rebate))
                    {
                        if (!commissions.Lookup.TryGetValue(exchange[1..].ToUpper(), out rebate))
                        {
                            if (OmsCore.RouteToExchangeLookup.TryGetValue(exchange[1..], out string[] exchanges))
                            {
                                double total = 0.0;
                                int count = 0;
                                foreach (string exch in exchanges)
                                {
                                    if (commissions.Lookup.TryGetValue(exch.ToUpper(), out double tempRebate))
                                    {
                                        total += tempRebate;
                                        count++;
                                    }
                                }
                                if (count > 0)
                                {
                                    rebate = total / count;
                                }
                                else
                                {
                                    rebate = isSingleLeg
                                        ? 0.30
                                        : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Average();
                                }
                            }
                            else
                            {
                                rebate = isSingleLeg
                                    ? 0.30
                                    : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Average();
                            }
                        }
                    }
                }
                for (int i = 0; i < legs.LegCount; i++)
                {
                    Instrument leg = legs.GetLeg(i);
                    totalFees += OmsCore.Config.BrokerageFee * leg.ratio;
                    totalFees += OmsCore.Config.OrfFee * leg.ratio;
                    if (exchange != null && exchange == "DCBOE" && underlying == "$SPX")
                    {
                        totalFees += OmsCore.Config.DashSPXFee * leg.ratio;
                    }
                    else if (exchange is not null and "ZPROLL")
                    {
                        totalFees += OmsCore.Config.VolantZprollFee * leg.ratio;
                    }
                    else if (exchange != null && exchange.StartsWith("B"))
                    {
                        totalFees += OmsCore.Config.VolantFee * leg.ratio;
                    }
                    else
                    {
                        totalFees += OmsCore.Config.DashFee * leg.ratio;
                    }

                    bool isSell = !leg.buySell;
                    if (isSell &&
                        underlying != "$SPX" &&
                        underlying != "$NDX" &&
                        underlying != "$RUT")
                    {
                        double avgPx = 0.0;
                        double secFee = OmsCore.Config.SecFee * avgPx * 100.0 * leg.ratio;
                        if (!double.IsNaN(secFee))
                        {
                            totalFees += secFee;
                        }
                    }

                    totalFees += rebate * leg.ratio;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OmsRequestFeeCalculation));
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorNA);
            }
            return totalFees / 100;
        }

        [ExcelFunction("Oms Request Fee Range")]
        public static object OmsRequestFeeRange(string symbol, string exchange)
        {
            double totalFeesMin = 0.0;
            double totalFeesMax = 0.0;
            try
            {
                double rebateMin = 0;
                double rebateMax = 0;
                SymbolCodec legs = new(symbol);
                bool isSingleLeg = legs.LegCount == 1;
                string underlying = legs.UnderlyingSymbol();
                if (underlying != null && OmsCore.Config.UnderlyingToCommissionsMap.TryGetValue(underlying, out Comms.Models.Data.Oms.Commission commissions))
                {
                    if (string.IsNullOrWhiteSpace(exchange))
                    {
                        rebateMin = isSingleLeg
                            ? 0.30
                            : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Min();
                        rebateMax = isSingleLeg
                            ? 0.30
                            : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Max();
                    }
                    else if (!commissions.Lookup.TryGetValue(exchange.ToUpper(), out rebateMin))
                    {
                        if (!commissions.Lookup.TryGetValue(exchange[1..].ToUpper(), out rebateMin))
                        {
                            if (OmsCore.RouteToExchangeLookup.TryGetValue(exchange[1..], out string[] exchanges))
                            {
                                double total = 0.0;
                                int count = 0;
                                foreach (string exch in exchanges)
                                {
                                    if (commissions.Lookup.TryGetValue(exch.ToUpper(), out double tempRebate))
                                    {
                                        total += tempRebate;
                                        count++;
                                    }
                                }
                                if (count > 0)
                                {
                                    rebateMin = total / count;
                                    rebateMax = rebateMin;
                                }
                                else
                                {
                                    rebateMin = isSingleLeg
                                        ? 0.30
                                        : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Min();
                                    rebateMax = isSingleLeg
                                        ? 0.30
                                        : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Max();
                                }
                            }
                            else
                            {
                                rebateMin = isSingleLeg
                                    ? 0.30
                                    : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Min();
                                rebateMax = isSingleLeg
                                    ? 0.30
                                    : OmsCore.Config.UnderlyingToCommissionsMap.Values.Where(x => x.IsPenny == commissions.IsPenny).SelectMany(x => x.Lookup.Values).Max();
                            }
                        }
                        else
                        {
                            rebateMax = rebateMin;
                        }
                    }
                }
                for (int i = 0; i < legs.LegCount; i++)
                {
                    Instrument leg = legs.GetLeg(i);
                    totalFeesMin += OmsCore.Config.BrokerageFee * leg.ratio;
                    totalFeesMin += OmsCore.Config.OrfFee * leg.ratio;
                    if (exchange != null && exchange == "DCBOE" && underlying == "$SPX")
                    {
                        totalFeesMin += OmsCore.Config.DashSPXFee * leg.ratio;
                    }
                    else if (exchange is not null and "ZPROLL")
                    {
                        totalFeesMin += OmsCore.Config.VolantZprollFee * leg.ratio;
                    }
                    else if (exchange != null && exchange.StartsWith("B"))
                    {
                        totalFeesMin += OmsCore.Config.VolantFee * leg.ratio;
                    }
                    else
                    {
                        totalFeesMin += OmsCore.Config.DashFee * leg.ratio;
                    }

                    bool isSell = !leg.buySell;
                    if (isSell &&
                        underlying != "$SPX" &&
                        underlying != "$NDX" &&
                        underlying != "$RUT")
                    {
                        double avgPx = 0.0;
                        double secFee = OmsCore.Config.SecFee * avgPx * 100.0 * leg.ratio;
                        if (!double.IsNaN(secFee))
                        {
                            totalFeesMin += secFee;
                        }
                    }

                    totalFeesMax = totalFeesMin;
                    totalFeesMin += rebateMin * leg.ratio;
                    totalFeesMax += rebateMax * leg.ratio;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OmsRequestFeeRange));
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorNA);
            }

            totalFeesMin /= 100;
            totalFeesMax /= 100;

            string range = totalFeesMin + "," + totalFeesMax;
            return range;
        }

        [ExcelFunction("Oms Send DomCommand")]
        public static object OmsSendDomCommand(
            [ExcelArgument(Description = "Dominator Command Code", Name = "CommandEnum")] string commandEnum,
            [ExcelArgument(Description = "Dominator Command Argument", Name = "DomArgument")] string domArgument)
        {
            return ExcelAsyncUtil.Run(nameof(OmsSendDomCommand), null, () => SendDomCommand(commandEnum, domArgument));
        }

        [ExcelFunction("Oms Subscribe Position")]
        public static object OmsSubscribePosition(
            [ExcelArgument(Name = "symbol")] string symbol)
        {
            symbol = symbol.Replace("`", "");
            return XlCall.RTD(OmsAddinRtdServer.PROG_ID, null, symbol, SubscriptionFieldType.FirmSymbolPosition.ToString());
        }

        [ExcelFunction("Oms Subscribe Order update")]
        public static object OmsSubscribeOrderUpdate(
            [ExcelArgument(Name = "Order Id")] string orderId)
        {
            orderId = orderId.Replace("`", "");
            return XlCall.RTD(OmsAddinRtdServer.PROG_ID, null, orderId, SubscriptionFieldType.OrderUpdate.ToString());
        }

        [ExcelFunction("Oms Send Order")]
        public static async Task<object> OmsSendOrder(
            [ExcelArgument(Name = "Condition")] string condition,
            [ExcelArgument(Name = "Account")] string account,
            [ExcelArgument(Name = "Symbol")] string symbol,
            [ExcelArgument(Name = "Route")] string route,
            [ExcelArgument(Name = "Side")] string side,
            [ExcelArgument(Name = "Order Type")] string orderType,
            [ExcelArgument(Name = "Qty")] int quantity,
            [ExcelArgument(Name = "Price")] double price,
            [ExcelArgument(Name = "TIF")] string tif,
            [ExcelArgument(Name = "Pos Effect")] string positionEffect,
            [ExcelArgument(Name = "Legs")] string legs,
            [ExcelArgument(Name = "Submit Delay")] int submitDelay = 0,
            [ExcelArgument(Name = "Details")] int details = 0,
            [ExcelArgument(Name = "Tag")] string tag = "",
            [ExcelArgument(Name = "Underlying")] string underlying = "",
            [ExcelArgument(Name = "Min Under Bid")] double minUnderBid = 0.0,
            [ExcelArgument(Name = "Min Under Ask")] double minUnderAsk = 0.0,
            [ExcelArgument(Name = "Cancel Delay")] int cancelDelay = 0,
            [ExcelArgument(Name = "Local ID")] string localID = "",
            [ExcelArgument(Name = "Trader")] string trader = "",
            [ExcelArgument(Name = "Edge")] double edge = 0.0,
            [ExcelArgument(Name = "Type")] string type = "",
            [ExcelArgument(Name = "SubType")] string subtype = "",
            [ExcelArgument(Name = "EMA")] double ema = 0.0,
            [ExcelArgument(Name = "TV")] double theo = 0.0,
            [ExcelArgument(Name = "Bid")] double bid = 0.0,
            [ExcelArgument(Name = "Ask")] double ask = 0.0)
        {
            try
            {
                if (OmsCore.User == null)
                {
                    throw new SlimException($"{nameof(OmsCore.User)} not logged in!");
                }
                if (!OmsCore.AutoTraderClient.IsConnected)
                {
                    throw new SlimException("Auto Trader Not Connected!");
                }
                if (condition.ToUpper() != "TRUE")
                {
                    throw new SlimException($"{nameof(condition)} not met!");
                }
                if (string.IsNullOrWhiteSpace(account))
                {
                    throw new SlimException($"{nameof(account)} missing");
                }
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    throw new SlimException($"{nameof(symbol)} missing");
                }
                if (string.IsNullOrWhiteSpace(route))
                {
                    throw new SlimException($"{nameof(route)} missing");
                }
                if (string.IsNullOrWhiteSpace(side) || !Enum.TryParse(side, true, out Side sideValue))
                {
                    throw new SlimException($"{nameof(side)} missing");
                }
                if (quantity < 1)
                {
                    throw new SlimException($"{nameof(quantity)} missing");
                }
                if (string.IsNullOrWhiteSpace(tif) || !Enum.TryParse(tif, true, out TimeInForce tifValue))
                {
                    tifValue = TimeInForce.DAY;
                }
                if (string.IsNullOrWhiteSpace(positionEffect) || !Enum.TryParse(positionEffect, true, out PositionEffect positionEffectValue))
                {
                    positionEffectValue = PositionEffect.AUTO;
                }
                if (submitDelay < 0)
                {
                    throw new SlimException($"Invalid {nameof(submitDelay)}");
                }
                if (cancelDelay < 0)
                {
                    throw new SlimException($"Invalid {nameof(cancelDelay)}");
                }
                if (string.IsNullOrWhiteSpace(localID))
                {
                    throw new SlimException($"{nameof(localID)} missing");
                }
                if (!Enum.TryParse(subtype, true, out SubType subTypeId))
                {
                    subTypeId = SubType.None;
                }
                var isComplex = orderType == "MLEG";
                var legSymbols = new SymbolCodec(legs);
                if ((isComplex && legSymbols.LegCount < 2) || (!isComplex && legSymbols.LegCount != 1))
                {
                    throw new SlimException($"Invalid {nameof(legs)} for type");
                }

                var order = isComplex ? new ComplexOrderSlim(OmsCore.SecurityBook) : new OrderSlim(OmsCore.SecurityBook);
                order.Destination = "Direct";
                order.Venue = Venue.Silexx;
                order.AccountAcronym = account;
                order.UnderlyingSymbol = symbol;
                order.Route = route;
                order.Side = sideValue;
                order.Quantity = quantity;
                order.Price = price;
                order.TimeInForce = tifValue;
                order.PositionEffect = positionEffectValue;
                order.NewToCancelTime = cancelDelay;
                order.Comment = tag;
                order.Tag = trader;
                order.Bid = bid;
                order.Ask = ask;
                order.DeltaAdjustedTheo = theo;
                order.Ema = ema;
                order.TagEdge = edge;
                order.LocalID = localID;
                order.TypeId = ModuleType.Dominator;
                order.OrderSource = OrderSource.AddIn;
                order.SubTypeId = subTypeId;

                if (isComplex)
                {
                    var complexOrder = (ComplexOrderSlim)order;
                    for (int i = 0; i < legSymbols.LegCount; i++)
                    {
                        var leg = legSymbols.GetLeg(i);
                        var legId = $"leg-{i}";
                        var complexOrderLeg = complexOrder.GetLeg(legId);
                        complexOrderLeg.Symbol = leg.ToTOS();
                        complexOrderLeg.Side = leg.buySell ? Side.Buy : Side.Sell;
                        complexOrderLeg.Ratio = leg.ratio;
                        complexOrderLeg.Quantity = leg.ratio * quantity;
                    }
                }
                else
                {
                    var leg = legSymbols.GetLeg(0);
                    order.Symbol = leg.ToTOS();
                }

                if (submitDelay > 0)
                {
                    await Task.Delay(submitDelay).ContinueWith(t => OmsCore.AutoTraderClient.SendOrder(order));
                }
                else
                {
                    OmsCore.AutoTraderClient.SendOrder(order);
                }

                return "Order Sent - " + order.LocalID;
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(OmsSendOrder));
                return $"Not Sent - {ex.Message}";
            }
        }

        private static object SendDomCommand(string commandCode, string argument)
        {
            try
            {
                OmsCore.DominatorClient.SendCommand(commandCode, argument);
                return true;
            }
            catch (Exception)
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsCalculateIvAsync(string symbol, double underlyingPrice, double optionPrice, DateTime dateTime)
        {
            Instrument leg = new(symbol);
            if (leg.valid)
            {
                string underlyingSymbol = leg.underlyingSymbol;

                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                PricingParameters pricingParameters = new()
                {
                    Volatility = 0.0,
                    PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                    Strike = leg.strike,
                    DaysToExpiration = (leg.expiration - dateTime).TotalDays,
                    RiskFreeRate = underlyingDetails.RiskFreeRate,
                    StockRate = underlyingDetails.StockRate,
                    UnderlyingPrice = underlyingPrice,
                    UnderlyingMultiplier = underlyingDetails.Multiplier,
                    ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                };
                pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, underlyingPrice, underlyingDetails.Dividends, dateTime);

                Greeks greeks = new();
                double iv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, optionPrice, greeks);
                return iv;
            }
            else
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsCalculateIvPriceAsync(string symbol, double underlyingPrice, double optionPrice, DateTime dateTime)
        {
            Instrument leg = new(symbol);
            if (leg.valid)
            {
                string underlyingSymbol = leg.underlyingSymbol;
                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                PricingParameters pricingParameters = new()
                {
                    Volatility = 0.0,
                    PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                    Strike = leg.strike,
                    DaysToExpiration = (leg.expiration - dateTime).TotalDays,
                    RiskFreeRate = underlyingDetails.RiskFreeRate,
                    StockRate = underlyingDetails.StockRate,
                    UnderlyingPrice = underlyingPrice,
                    UnderlyingMultiplier = underlyingDetails.Multiplier,
                    ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                };
                pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, underlyingPrice, underlyingDetails.Dividends, dateTime);

                Greeks greeks = new();
                double iv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, optionPrice, greeks);
                pricingParameters.Volatility = iv;
                double price = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                return price;
            }
            else
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsCalculateOptionPriceAsync(string symbol, double iv, double underlyingPrice, DateTime dateTime)
        {
            Instrument leg = new(symbol);
            if (leg.valid)
            {
                string underlyingSymbol = leg.underlyingSymbol;
                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                if (underlyingDetails == null)
                {
                    return "Loading underlying info failed.";
                }

                PricingParameters pricingParameters = new()
                {
                    Volatility = iv,
                    PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                    Strike = leg.strike,
                    DaysToExpiration = (leg.expiration - dateTime).TotalDays,
                    RiskFreeRate = underlyingDetails.RiskFreeRate,
                    StockRate = underlyingDetails.StockRate,
                    UnderlyingPrice = underlyingPrice,
                    UnderlyingMultiplier = underlyingDetails.Multiplier,
                    ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                };
                pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, underlyingPrice, underlyingDetails.Dividends, dateTime);

                Greeks greeks = new();
                pricingParameters.Volatility = iv;
                double price = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                return price;
            }
            else
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsRequestBestEdgeToTheoAsync(string symbol)
        {
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount > 0)
            {
                string underlying = symbolCodec.UnderlyingSymbol();
                StrategyDispatcher.TryIdentify(symbol, out var baseStrategy, out _, out _);
                if (baseStrategy is BaseStrategy.INVALID or BaseStrategy.CUSTOM)
                {
                    return "Strategy not supported";
                }
                else
                {
                    int time = 0;
                    for (int i = 0; i < symbolCodec.LegCount; i++)
                    {
                        Instrument leg = symbolCodec.GetLeg(i);
                        if (leg.symbol.StartsWith('.'))
                        {
                            time += leg.expiration.Date.GetHashCode();
                        }
                        else
                        {
                            return "Invalid Symbol";
                        }
                    }
                    Task<Hercules.Client.BestEdgeToTheo> task = OmsCore.HerculesClient.RequestBestEdgeToTheoAsync(underlying, baseStrategy, time);
                    task.Wait();
                    Hercules.Client.BestEdgeToTheo bestEdgeToTheo = task.Result;
                    if (bestEdgeToTheo.lastBuyEdgeToTheoTime == default && bestEdgeToTheo.lastSellEdgeToTheoTime == default)
                    {
                        return "Request failed!";
                    }
                    else
                    {
                        return "BestBuy," + bestEdgeToTheo.bestBuyEdgeToTheo.ToString("n2") + ";AvgBuy," + bestEdgeToTheo.avgBuyEdgeToTheo.ToString("n2") + ";LastBuy," + bestEdgeToTheo.lastBuyEdgeToTheo.ToString("n2") + ";LastBuyTime," + bestEdgeToTheo.lastBuyEdgeToTheoTime.ToString("hh:mm:ss") + "BestSell," + bestEdgeToTheo.bestSellEdgeToTheo.ToString("n2") + ";AvgSell," + bestEdgeToTheo.avgSellEdgeToTheo.ToString("n2") + ";LastSell," + bestEdgeToTheo.lastSellEdgeToTheo.ToString("n2") + ";LastSellTime," + bestEdgeToTheo.lastSellEdgeToTheoTime.ToString("hh:mm:ss");
                    }
                }
            }
            else
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsRequestIvChainAsync(string symbol, double days)
        {
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount > 0)
            {
                Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();

                days = Math.Min(days, MAX_REQUEST_DAYS);
                TimeSpan span = TimeSpan.FromDays(days);
                DateTime endDateTime = DateTime.Now;
                DateTime startDateTime = endDateTime - span;
                int weekend = TimeHelper.GetWeekendDaysBetween(startDateTime, endDateTime);
                if (weekend > 0)
                {
                    startDateTime -= TimeSpan.FromDays(weekend);
                }

                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);

                    if (!leg.valid)
                    {
                        continue;
                    }

                    Task<List<OptionSnapshot>> task = OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    task.Wait();
                    List<OptionSnapshot> results = task.Result;
                    if (results != null)
                    {
                        foreach (OptionSnapshot result in results)
                        {
                            if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapTime,
                                };
                                snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                            }
                            int ratio = leg.buySell ? leg.ratio : -leg.ratio;
                            dataPoint.AddResult(i, ratio, result);
                        }
                    }
                }

                List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values
                    .Where(chartDataPoint => chartDataPoint.TryCalculate(symbolCodec.LegCount))
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();
                string value = ";" + string.Join(";", dataPoints.Select(x => x.Timestamp.ToString("hh:mm:ss") + "," + x.BidIv + "," + x.Iv + "," + x.AskIv + "," + x.UnderPx));
                return value;
            }
            else
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object RequestMarketCrossScan(double lookbackInSeconds, double minMarketCross, double currentMarketWidth)
        {
            try
            {
                List<Models.Data.Responses.MarketCrossScanResult> results = OmsCore.EdgeScannerClient.RequestMarketCrossScan(lookbackInSeconds, minMarketCross, currentMarketWidth);
                if (results.Count > 0)
                {
                    string output = string.Join(";", results.Select(x => x.Symbol + "," + x.HighestBid.ToString("n2") + "," + x.LowestAsk.ToString("n2") + "," + x.UnderMid.ToString("n2")));
                    return output;
                }
                else
                {
                    return "No Results Found";
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestMarketCrossScan));
                return "Request Failed";
            }
        }

        private static object OmsRequestSymbolFishStatusAsync(string symbol)
        {
            try
            {
                SymbolCodec symbolCodec = new(symbol);
                if (symbolCodec.LegCount > 0)
                {
                    symbolCodec.Normalize();
                    Models.Data.Responses.SymbolFishStatusResponse results = OmsCore.HerculesClient.RequestSymbolFishStatus(symbol);
                    if (results != null)
                    {
                        double totalDays = (DateTime.Now - results.LastFishTime).TotalDays;
                        return "Symbol," + symbolCodec.ToTOS() + ";Status," + results.FishStatus.ToString() + ";Level," + results.FishLevel.ToString("N2") + ";Edge," + results.FishEdge.ToString("N2") + ";FishDate," + (totalDays is <= 10 and > 0 ? totalDays.ToString("N0") : 0) + ";FishTime," + (totalDays <= 10 ? results.LastFishTime.ToString("hh:mm:ss") : "00:00:00");
                    }
                    else
                    {
                        return "No Results Found";
                    }
                }
                else
                {
                    return "Invalid symbol";
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OmsRequestSymbolFishStatusAsync));
                return "Request Failed";
            }
        }

        private static object OmsRequestTightestEdgeToTheoAsync(string symbol, double days, double price = double.NaN)
        {
            try
            {
                SymbolCodec symbolCodec = new(symbol);
                if (symbolCodec.LegCount <= 0)
                {
                    return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
                }
                else
                {
                    List<DataPointModel> dataPoints = GetRecalculatedIvDataPoints(days, price, symbolCodec);
                    DataPointModel minBid = dataPoints.MinBy(x => Math.Abs(x.BidIv - x.Iv));
                    DataPointModel minAsk = dataPoints.MinBy(x => Math.Abs(x.AskIv - x.Iv));
                    List<DataPointModel> output = new();
                    if (minBid != null)
                    {
                        output.Add(minBid);
                    }
                    if (minAsk != null)
                    {
                        output.Add(minAsk);
                    }
                    string value = ";" + string.Join(";", output.Select(x => x.Timestamp.ToString("hh:mm:ss") + "," + x.BidIv + "," + x.Iv + "," + x.AskIv + "," + x.UnderPx));
                    return value;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static object OmsRequestEdgeSummaryAsync(string symbol, double days, double mins, double price = double.NaN, double percentFromUnder = 1, bool skipOpenClose = true, bool groupByDay = false)
        {
            _log.Info("Start Request For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Days: " + days + ", " +
                                  "Mins: " + mins + ", " +
                                  "Price: " + price + ", " +
                                  "Percent: " + percentFromUnder + ", " +
                                  "SkipOC: " + skipOpenClose + ".");
            Stopwatch globalStopwatch = Stopwatch.StartNew();
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount > 0)
            {
                string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                if (underlyingDetails == null)
                {
                    return "Loading underlying info failed. Symbol: " + underlyingSymbol;
                }

                double midPrice;
                if (!double.IsNaN(price) && price > 0)
                {
                    midPrice = price;
                }
                else
                {
                    double bid = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Bid);
                    double ask = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Ask);
                    midPrice = (bid + ask) / 2;

                    _log.Info("Price Calculated For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Days: " + days + ", " +
                              "Mins: " + mins + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ", " +
                              "SkipOC: " + skipOpenClose + ".");
                }

                Dictionary<DateTime, DataPointModel> snapTimeToDataPointMap = new();
                days = Math.Min(days, MAX_REQUEST_DAYS);
                DateTime targetStartDay = (DateTime.Now - TimeSpan.FromDays(days)).Date;
                switch (targetStartDay.DayOfWeek)
                {
                    case DayOfWeek.Saturday:
                        targetStartDay -= TimeSpan.FromDays(1);
                        break;
                    case DayOfWeek.Sunday:
                        targetStartDay -= TimeSpan.FromDays(2);
                        break;
                }

                DateTime marketClose = targetStartDay.Date + TimeSpan.FromHours(15);
                if (underlyingSymbol.StartsWith("$"))
                {
                    marketClose += TimeSpan.FromMinutes(15);
                }

                DateTime endDateTime = marketClose > DateTime.Now ? DateTime.Now : marketClose;
                DateTime startDateTime = endDateTime - TimeSpan.FromMinutes(mins);

                marketClose = DateTime.Today + TimeSpan.FromHours(15);
                if (underlyingSymbol.StartsWith("$"))
                {
                    marketClose += TimeSpan.FromMinutes(15);
                }
                endDateTime = marketClose > DateTime.Now ? DateTime.Now : marketClose;

                if (skipOpenClose)
                {
                    DateTime skippedMarketOpen = targetStartDay + TimeSpan.FromHours(8.5) + TimeSpan.FromMinutes(2);
                    if (startDateTime < skippedMarketOpen)
                    {
                        startDateTime = skippedMarketOpen;
                    }

                    DateTime skippedMarketClose = marketClose - TimeSpan.FromMinutes(2);
                    if (endDateTime > skippedMarketClose)
                    {
                        endDateTime = skippedMarketClose;
                    }
                }
                else
                {
                    DateTime marketOpen = targetStartDay + TimeSpan.FromHours(8.5);
                    if (startDateTime < marketOpen)
                    {
                        startDateTime = marketOpen;
                    }
                }

                _log.Info("Time Range Selected For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + midPrice + ", " +
                          "Percent: " + percentFromUnder + ".");

                int totalSamples = 0;
                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);

                    if (!leg.valid)
                    {
                        _log.Info("Invalid Leg For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                  "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "Price: " + midPrice + ", " +
                                  "Percent: " + percentFromUnder + ".");

                        continue;
                    }

                    int ratio = leg.buySell ? leg.ratio : -leg.ratio;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    List<OptionSnapshot> results = OmsCore.GatewayClient.RequestOptionSnapshots(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    stopwatch.Stop();
                    _log.Info("Response Received For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                              "Snapshots Found: " + results?.Count + ", " +
                              "Elapsed: " + stopwatch.ElapsedMilliseconds + "ms, " +
                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");

                    if (results != null)
                    {
                        totalSamples = Math.Max(totalSamples, results.Count);
                        stopwatch = Stopwatch.StartNew();
                        double bid = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Bid);
                        double ask = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Ask);
                        double iv = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.ImpliedVol);
                        double vega = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Vega);
                        stopwatch.Stop();
                        _log.Info("Requesting Current Market Data Snapshots For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                  "Elapsed: " + stopwatch.ElapsedMilliseconds + "ms, " +
                                  "Bid: " + bid + ", " +
                                  "Ask: " + ask + ", " +
                                  "Iv: " + iv + ", " +
                                  "Vega: " + vega + ", " +
                                  "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "Price: " + midPrice + ", " +
                                  "Percent: " + percentFromUnder + ".");

                        results.Add(new OptionSnapshot()
                        {
                            SnapTime = DateTime.Now,
                            UnderAsk1 = midPrice,
                            UnderBid1 = midPrice,
                            Bid = bid,
                            Ask = ask,
                            HwIV = iv,
                            HwVega = vega,
                        });

                        foreach (OptionSnapshot result in results)
                        {
                            if (skipOpenClose)
                            {
                                DateTime skippedMarketOpen = result.SnapTime.Date + TimeSpan.FromHours(8.5) + TimeSpan.FromMinutes(2);
                                if (result.SnapTime < skippedMarketOpen)
                                {
                                    _log.Info("Skipping Result Snapshot For Edge Summary. " +
                                              "Symbol: " + symbol + ", " +
                                              "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                              "Market Open: " + skippedMarketOpen.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Snapshot Time: " + result.SnapTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Price: " + midPrice + ", " +
                                              "Percent: " + percentFromUnder + ".");
                                    continue;
                                }

                                DateTime skippedMarketClose = result.SnapTime.Date + TimeSpan.FromHours(15) - TimeSpan.FromMinutes(2);
                                if (underlyingSymbol.StartsWith("$"))
                                {
                                    skippedMarketClose += TimeSpan.FromMinutes(15);
                                }
                                if (result.SnapTime > skippedMarketClose)
                                {
                                    _log.Info("Skipping Result Snapshot For Edge Summary. " +
                                              "Symbol: " + symbol + ", " +
                                              "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                              "Market Close: " + skippedMarketClose.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Snapshot Time: " + result.SnapTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Price: " + midPrice + ", " +
                                              "Percent: " + percentFromUnder + ".");
                                    continue;
                                }
                            }

                            double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                            double percentageDifference = Math.Abs((midPrice - resultMid) / ((midPrice + resultMid) / 2));
                            if (percentageDifference > percentFromUnder)
                            {
                                _log.Info("Skipping Result Snapshot By Price For Edge Summary. " +
                                          "Symbol: " + symbol + ", " +
                                          "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                          "Snapshot Mid: " + resultMid + ", " +
                                          "Snapshot Time: " + result.SnapTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Price: " + midPrice + ", " +
                                          "Percent: " + percentFromUnder + ".");
                                continue;
                            }

                            if (!snapTimeToDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapTime,
                                };
                                snapTimeToDataPointMap[result.SnapTime] = dataPoint;
                            }
                            PricingParameters pricingParameters = new()
                            {
                                Volatility = 0.0,
                                PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                Strike = leg.strike,
                                DaysToExpiration = (leg.expiration - result.SnapTime).TotalDays,
                                RiskFreeRate = underlyingDetails.RiskFreeRate,
                                StockRate = underlyingDetails.StockRate,
                                UnderlyingPrice = resultMid,
                                UnderlyingMultiplier = underlyingDetails.Multiplier,
                                ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                            };
                            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapTime);
                            Greeks greeks = new();
                            dataPoint.UnderPx = resultMid;

                            double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                            pricingParameters.Volatility = bidIv;
                            double bidPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            bidPrice += (midPrice - resultMid) * greeks.Delta;
                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                            double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.HwTV, greeks);
                            pricingParameters.Volatility = hwIv;
                            double hwTheo = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            hwTheo += (midPrice - resultMid) * greeks.Delta;
                            dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwTheo, result);

                            double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                            pricingParameters.Volatility = askIv;
                            double askPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            askPrice += (midPrice - resultMid) * greeks.Delta;
                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                        }
                    }
                }

                List<DataPointModel> dataPoints = snapTimeToDataPointMap.Values
                    .Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount))
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();

                string output = "";

                if (!groupByDay)
                {
                    DataPointModel highestBid = dataPoints.MaxBy(x => x.BidIv);
                    DataPointModel lowestAsk = dataPoints.MinBy(x => x.AskIv);
                    DataPointModel tightestBidToTheo = dataPoints.MinBy(x => Math.Abs(x.BidIv - x.Iv));
                    DataPointModel tightestAskToTheo = dataPoints.MinBy(x => Math.Abs(x.AskIv - x.Iv));

                    output = "HighestBid," + (highestBid != null ? Math.Round(highestBid.BidIv, 3) : "") +
                             ";LowestOffer," + (lowestAsk != null ? Math.Round(lowestAsk.AskIv, 3) : "") +
                             ";TightestBidToTheo," + (tightestBidToTheo != null ? Math.Round(Math.Abs(tightestBidToTheo.BidIv - tightestBidToTheo.Iv), 3) : "") +
                             ";TightestAskToTheo," + (tightestAskToTheo != null ? Math.Round(Math.Abs(tightestAskToTheo.AskIv - tightestAskToTheo.Iv), 3) : "") +
                             ";Underlying," + midPrice +
                             ";TotalSamples," + totalSamples +
                             ";SelectedSamples," + dataPoints.Count;
                }
                else
                {
                    IEnumerable<IGrouping<DateTime, DataPointModel>> groupedDataPoints = dataPoints.GroupBy(x => x.Timestamp.Date);

                    foreach (IGrouping<DateTime, DataPointModel> group in groupedDataPoints.OrderBy(x => x.Key))
                    {
                        DateTime time = group.Key;
                        List<DataPointModel> groupDatapoints = group.ToList();

                        DataPointModel highestBid = groupDatapoints.MaxBy(x => x.BidIv);
                        DataPointModel lowestAsk = groupDatapoints.MinBy(x => x.AskIv);
                        DataPointModel tightestBidToTheo = groupDatapoints.MinBy(x => Math.Abs(x.BidIv - x.Iv));
                        DataPointModel tightestAskToTheo = groupDatapoints.MinBy(x => Math.Abs(x.AskIv - x.Iv));

                        string groupOutput = ";HighestBid," + (highestBid != null ? Math.Round(highestBid.BidIv, 3) : "") +
                                          ";LowestOffer," + (lowestAsk != null ? Math.Round(lowestAsk.AskIv, 3) : "") +
                                          ";TightestBidToTheo," + (tightestBidToTheo != null ? Math.Round(Math.Abs(tightestBidToTheo.BidIv - tightestBidToTheo.Iv), 3) : "") +
                                          ";TightestAskToTheo," + (tightestAskToTheo != null ? Math.Round(Math.Abs(tightestAskToTheo.AskIv - tightestAskToTheo.Iv), 3) : "") +
                                          ";Underlying," + midPrice +
                                          ";TotalSamples," + totalSamples +
                                          ";SelectedSamples," + groupDatapoints.Count;

                        output += "[" + "Date, " + time.ToString("dd-MM-yy") + groupOutput + "]";
                    }
                }

                globalStopwatch.Stop();
                _log.Info("Valid Datapoints For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Datapoints: " + dataPoints.Count + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + midPrice + ", " +
                          "Percent: " + percentFromUnder + ", " +
                          "Output: " + output + ".");

                return output;
            }
            else
            {
                globalStopwatch.Stop();
                _log.Info("Request For Edge Summary Failed. " +
                          "No valid legs. " +
                          "Symbol: " + symbol + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Days: " + days + ", " +
                          "Mins: " + mins + ", " +
                          "Price: " + price + ", " +
                          "Percent: " + percentFromUnder + ", " +
                          "SkipOC: " + skipOpenClose + ".");
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsRequestAdjustedEdgeSummaryAsync(string symbol, double mins, double price = double.NaN, double percentFromUnder = 1, bool withDetails = false)
        {
            _log.Info("Start Request For Edge Summary. " +
                      "Symbol: " + symbol + ", " +
                      "Mins: " + mins + ", " +
                      "Price: " + price + ", " +
                      "Percent: " + percentFromUnder + ".");
            Stopwatch globalStopwatch = Stopwatch.StartNew();
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount > 0)
            {
                string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                if (underlyingDetails == null)
                {
                    return "Loading underlying info failed. Symbol: " + underlyingSymbol;
                }

                double midPrice;
                if (!double.IsNaN(price) && price > 0)
                {
                    midPrice = price;
                }
                else
                {
                    double bid = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Bid);
                    double ask = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Ask);
                    midPrice = (bid + ask) / 2;

                    _log.Info("Price Calculated For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Mins: " + mins + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");
                }

                Dictionary<DateTime, DataPointModel> snapTimeToDataPointMap = new();
                DateTime targetStartDay = DateTime.Today;
                switch (targetStartDay.DayOfWeek)
                {
                    case DayOfWeek.Saturday:
                        targetStartDay -= TimeSpan.FromDays(1);
                        break;
                    case DayOfWeek.Sunday:
                        targetStartDay -= TimeSpan.FromDays(2);
                        break;
                }

                DateTime marketClose = targetStartDay.Date + TimeSpan.FromHours(15);
                if (underlyingSymbol.StartsWith("$"))
                {
                    marketClose += TimeSpan.FromMinutes(15);
                }

                DateTime endDateTime = marketClose > DateTime.Now ? DateTime.Now : marketClose;
                DateTime startDateTime = endDateTime - TimeSpan.FromMinutes(mins);

                _log.Info("Time Range Selected For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + midPrice + ", " +
                          "Percent: " + percentFromUnder + ".");

                int totalSamples = 0;
                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);

                    if (!leg.valid)
                    {
                        _log.Info("Invalid Leg For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                  "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "Price: " + midPrice + ", " +
                                  "Percent: " + percentFromUnder + ".");

                        continue;
                    }

                    int ratio = leg.buySell ? leg.ratio : -leg.ratio;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    List<Models.Data.Responses.OptionSnapshot> results = OmsCore.EdgeScannerClient.RequestOptionSnapshots(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    stopwatch.Stop();
                    _log.Info("Response Received For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                              "Snapshots Found: " + results?.Count + ", " +
                              "Elapsed: " + stopwatch.ElapsedMilliseconds + "ms, " +
                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");

                    if (results != null)
                    {
                        totalSamples = Math.Max(totalSamples, results.Count);
                        double theo = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.TheorethicalValue);

                        for (int j = 0; j < results.Count; j++)
                        {
                            Models.Data.Responses.OptionSnapshot result = results[j];
                            _log.Info("Result Snapshot For Edge Summary. " +
                                      "Symbol: " + symbol + ", " +
                                      "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                      "Update: " + (j + 1) + ", " +
                                      "Bid: " + result.Bid + ", " +
                                      "Ask: " + result.Ask + ", " +
                                      "UnderBid: " + result.UnderBid + ", " +
                                      "UnderAsk: " + result.UnderAsk + ", " +
                                      "AdjTheo: " + result.AdjTheo + ", " +
                                      "Theo: " + result.Theo + ", " +
                                      "Delta: " + result.Delta + ", " +
                                      "Vega: " + result.Vega + ", " +
                                      "Iv: " + result.Iv + ", " +
                                      "QuoteTime: " + result.QuoteTime + ", " +
                                      "SnapshotTime: " + result.SnapshotTime + ", " +
                                      "HanweckCalcTime: " + result.HanweckCalcTime + ", " +
                                      "AdjTheoTime: " + result.AdjTheoTime + ", " +
                                      "UnderMid: " + result.UnderMid + ", " +
                                      "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ".");


                            double resultMid = (result.UnderAsk + result.UnderBid) / 2;
                            double percentageDifference = Math.Abs((midPrice - resultMid) / ((midPrice + resultMid) / 2));
                            if (percentageDifference > percentFromUnder)
                            {
                                _log.Info("Skipping Result Snapshot By Price For Edge Summary. " +
                                          "Symbol: " + symbol + ", " +
                                          "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                          "Snapshot Mid: " + resultMid + ", " +
                                          "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Price: " + midPrice + ", " +
                                          "Percent: " + percentFromUnder + ".");
                                continue;
                            }

                            if (!snapTimeToDataPointMap.TryGetValue(result.SnapshotTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapshotTime,
                                };
                                snapTimeToDataPointMap[result.SnapshotTime] = dataPoint;
                            }
                            PricingParameters pricingParameters = new()
                            {
                                Volatility = 0.0,
                                PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                Strike = leg.strike,
                                DaysToExpiration = (leg.expiration - result.SnapshotTime).TotalDays,
                                RiskFreeRate = underlyingDetails.RiskFreeRate,
                                StockRate = underlyingDetails.StockRate,
                                UnderlyingPrice = resultMid,
                                UnderlyingMultiplier = underlyingDetails.Multiplier,
                                ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                            };
                            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapshotTime);
                            Greeks greeks = new();
                            dataPoint.UnderPx = resultMid;

                            double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                            pricingParameters.Volatility = bidIv;
                            double bidPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            bidPrice += (midPrice - resultMid) * greeks.Delta;
                            double adjTheo = result.AdjTheo + ((midPrice - resultMid) * greeks.Delta);

                            if (theo < adjTheo)
                            {
                                double change = adjTheo - theo;
                                bidPrice -= change;
                            }

                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                            double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Theo, greeks);
                            pricingParameters.Volatility = hwIv;
                            double hwTheo = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            hwTheo += (midPrice - resultMid) * greeks.Delta;
                            dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwTheo, result);

                            double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                            pricingParameters.Volatility = askIv;
                            double askPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            askPrice += (midPrice - resultMid) * greeks.Delta;

                            if (theo > adjTheo)
                            {
                                double change = theo - adjTheo;
                                askPrice += change;
                            }

                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                        }
                    }
                }

                List<DataPointModel> dataPoints = snapTimeToDataPointMap.Values
                    .Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount))
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();

                string output;

                DataPointModel highestBid = dataPoints.MaxBy(x => x.BidIv);
                DataPointModel lowestAsk = dataPoints.MinBy(x => x.AskIv);
                DataPointModel tightestBidToTheo = dataPoints.MinBy(x => Math.Abs(x.BidIv - x.Iv));
                DataPointModel tightestAskToTheo = dataPoints.MinBy(x => Math.Abs(x.AskIv - x.Iv));
                if (withDetails)
                {
                    output = "HighestBid," + (highestBid != null ? Math.Round(highestBid.BidIv, 3) : "") +
                             ";LowestOffer," + (lowestAsk != null ? Math.Round(lowestAsk.AskIv, 3) : "") +
                             ";TightestBidToTheo," + (tightestBidToTheo != null ? Math.Round(Math.Abs(tightestBidToTheo.BidIv - tightestBidToTheo.Iv), 3) : "") +
                             ";TightestAskToTheo," + (tightestAskToTheo != null ? Math.Round(Math.Abs(tightestAskToTheo.AskIv - tightestAskToTheo.Iv), 3) : "") +
                             ";HighestBidTime," + (highestBid != null ? highestBid.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                             ";LowestOfferTime," + (lowestAsk != null ? lowestAsk.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                             ";TightestBidToTheoTime," + (tightestBidToTheo != null ? tightestBidToTheo.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                             ";TightestAskToTheoTime," + (tightestAskToTheo != null ? tightestAskToTheo.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                             ";Underlying," + Math.Round(midPrice, 3) +
                             ";TotalSamples," + totalSamples +
                             ";SelectedSamples," + dataPoints.Count;
                }
                else
                {
                    output = "HighestBid," + (highestBid != null ? Math.Round(highestBid.BidIv, 3) : "") +
                             ";LowestOffer," + (lowestAsk != null ? Math.Round(lowestAsk.AskIv, 3) : "") +
                             ";TightestBidToTheo," + (tightestBidToTheo != null ? Math.Round(Math.Abs(tightestBidToTheo.BidIv - tightestBidToTheo.Iv), 3) : "") +
                             ";TightestAskToTheo," + (tightestAskToTheo != null ? Math.Round(Math.Abs(tightestAskToTheo.AskIv - tightestAskToTheo.Iv), 3) : "") +
                             ";Underlying," + Math.Round(midPrice, 3) +
                             ";TotalSamples," + totalSamples +
                             ";SelectedSamples," + dataPoints.Count;
                }

                globalStopwatch.Stop();
                _log.Info("Valid Datapoints For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Datapoints: " + dataPoints.Count + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + Math.Round(midPrice, 2) + ", " +
                          ";HighestBidTime," + (highestBid != null ? highestBid.Timestamp.ToString("hh: mm: ss.ffff") : "") +
                          ";LowestOfferTime," + (lowestAsk != null ? lowestAsk.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                          ";TightestBidToTheoTime," + (tightestBidToTheo != null ? tightestBidToTheo.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                          ";TightestAskToTheoTime," + (tightestAskToTheo != null ? tightestAskToTheo.Timestamp.ToString("hh:mm:ss.ffff") : "") +
                          "Percent: " + percentFromUnder + ", " +
                          "Output: " + output + ".");

                return output;
            }
            else
            {
                globalStopwatch.Stop();
                _log.Info("Request For Edge Summary Failed. " +
                          "No valid legs. " +
                          "Symbol: " + symbol + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Mins: " + mins + ", " +
                          "Price: " + price + ", " +
                          "Percent: " + percentFromUnder + ".");
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsRequestEdgeSummaryMemAsync(string symbol, double mins, double price = double.NaN, double percentFromUnder = 1)
        {
            _log.Info("Start Request For Edge Summary. " +
                      "Symbol: " + symbol + ", " +
                      "Mins: " + mins + ", " +
                      "Price: " + price + ", " +
                      "Percent: " + percentFromUnder + ".");
            Stopwatch globalStopwatch = Stopwatch.StartNew();
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount > 0)
            {
                string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                if (underlyingDetails == null)
                {
                    return "Loading underlying info failed. Symbol: " + underlyingSymbol;
                }

                double midPrice;
                if (!double.IsNaN(price) && price > 0)
                {
                    midPrice = price;
                }
                else
                {
                    double bid = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Bid);
                    double ask = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Ask);
                    midPrice = (bid + ask) / 2;

                    _log.Info("Price Calculated For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Mins: " + mins + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");
                }

                Dictionary<DateTime, DataPointModel> snapTimeToDataPointMap = new();
                DateTime targetStartDay = DateTime.Today;
                switch (targetStartDay.DayOfWeek)
                {
                    case DayOfWeek.Saturday:
                        targetStartDay -= TimeSpan.FromDays(1);
                        break;
                    case DayOfWeek.Sunday:
                        targetStartDay -= TimeSpan.FromDays(2);
                        break;
                }

                DateTime marketClose = targetStartDay.Date + TimeSpan.FromHours(15);
                if (underlyingSymbol.StartsWith("$"))
                {
                    marketClose += TimeSpan.FromMinutes(15);
                }

                DateTime endDateTime = marketClose > DateTime.Now ? DateTime.Now : marketClose;
                DateTime startDateTime = endDateTime - TimeSpan.FromMinutes(mins);

                _log.Info("Time Range Selected For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + midPrice + ", " +
                          "Percent: " + percentFromUnder + ".");

                int totalSamples = 0;
                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);

                    if (!leg.valid)
                    {
                        _log.Info("Invalid Leg For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                  "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "Price: " + midPrice + ", " +
                                  "Percent: " + percentFromUnder + ".");

                        continue;
                    }

                    int ratio = leg.buySell ? leg.ratio : -leg.ratio;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    List<Models.Data.Responses.OptionSnapshot> results = OmsCore.EdgeScannerClient.RequestOptionSnapshots(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    stopwatch.Stop();
                    _log.Info("Response Received For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                              "Snapshots Found: " + results?.Count + ", " +
                              "Elapsed: " + stopwatch.ElapsedMilliseconds + "ms, " +
                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");

                    if (results != null)
                    {
                        totalSamples = Math.Max(totalSamples, results.Count);

                        for (int j = 0; j < results.Count; j++)
                        {
                            Models.Data.Responses.OptionSnapshot result = results[j];
                            _log.Info("Result Snapshot For Edge Summary. " +
                                      "Symbol: " + symbol + ", " +
                                      "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                      "Update: " + (j + 1) + ", " +
                                      "Bid: " + result.Bid + ", " +
                                      "Ask: " + result.Ask + ", " +
                                      "UnderBid: " + result.UnderBid + ", " +
                                      "UnderAsk: " + result.UnderAsk + ", " +
                                      "AdjTheo: " + result.AdjTheo + ", " +
                                      "Theo: " + result.Theo + ", " +
                                      "Delta: " + result.Delta + ", " +
                                      "Vega: " + result.Vega + ", " +
                                      "Iv: " + result.Iv + ", " +
                                      "QuoteTime: " + result.QuoteTime + ", " +
                                      "SnapshotTime: " + result.SnapshotTime + ", " +
                                      "HanweckCalcTime: " + result.HanweckCalcTime + ", " +
                                      "AdjTheoTime: " + result.AdjTheoTime + ", " +
                                      "UnderMid: " + result.UnderMid + ", " +
                                      "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ".");


                            double resultMid = (result.UnderAsk + result.UnderBid) / 2;
                            double percentageDifference = Math.Abs((midPrice - resultMid) / ((midPrice + resultMid) / 2));
                            if (percentageDifference > percentFromUnder)
                            {
                                _log.Info("Skipping Result Snapshot By Price For Edge Summary. " +
                                          "Symbol: " + symbol + ", " +
                                          "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                          "Snapshot Mid: " + resultMid + ", " +
                                          "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Price: " + midPrice + ", " +
                                          "Percent: " + percentFromUnder + ".");
                                continue;
                            }

                            if (!snapTimeToDataPointMap.TryGetValue(result.SnapshotTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapshotTime,
                                };
                                snapTimeToDataPointMap[result.SnapshotTime] = dataPoint;
                            }
                            PricingParameters pricingParameters = new()
                            {
                                Volatility = 0.0,
                                PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                Strike = leg.strike,
                                DaysToExpiration = (leg.expiration - result.SnapshotTime).TotalDays,
                                RiskFreeRate = underlyingDetails.RiskFreeRate,
                                StockRate = underlyingDetails.StockRate,
                                UnderlyingPrice = resultMid,
                                UnderlyingMultiplier = underlyingDetails.Multiplier,
                                ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                            };
                            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapshotTime);
                            Greeks greeks = new();
                            dataPoint.UnderPx = resultMid;

                            double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                            pricingParameters.Volatility = bidIv;
                            double bidPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            bidPrice += (midPrice - resultMid) * greeks.Delta;

                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                            double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Theo, greeks);
                            pricingParameters.Volatility = hwIv;
                            double hwTheo = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            hwTheo += (midPrice - resultMid) * greeks.Delta;
                            dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwTheo, result);

                            double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                            pricingParameters.Volatility = askIv;
                            double askPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            askPrice += (midPrice - resultMid) * greeks.Delta;

                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                        }
                    }
                }
                List<DataPointModel> dataPoints = snapTimeToDataPointMap.Values
                    .Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount))
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();

                string output;

                DataPointModel highestBid = dataPoints.MaxBy(x => x.BidIv);
                DataPointModel lowestAsk = dataPoints.MinBy(x => x.AskIv);
                DataPointModel tightestBidToTheo = dataPoints.MinBy(x => Math.Abs(x.BidIv - x.Iv));
                DataPointModel tightestAskToTheo = dataPoints.MinBy(x => Math.Abs(x.AskIv - x.Iv));

                output = "HighestBid," + (highestBid != null ? Math.Round(highestBid.BidIv, 3) : "") +
                         ";LowestOffer," + (lowestAsk != null ? Math.Round(lowestAsk.AskIv, 3) : "") +
                         ";TightestBidToTheo," + (tightestBidToTheo != null ? Math.Round(Math.Abs(tightestBidToTheo.BidIv - tightestBidToTheo.Iv), 3) : "") +
                         ";TightestAskToTheo," + (tightestAskToTheo != null ? Math.Round(Math.Abs(tightestAskToTheo.AskIv - tightestAskToTheo.Iv), 3) : "") +
                         ";Underlying," + Math.Round(midPrice, 3) +
                         ";TotalSamples," + totalSamples +
                         ";SelectedSamples," + dataPoints.Count;


                globalStopwatch.Stop();
                _log.Info("Valid Datapoints For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Datapoints: " + dataPoints.Count + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + Math.Round(midPrice, 2) + ", " +
                          "Percent: " + percentFromUnder + ", " +
                          "Output: " + output + ".");

                return output;
            }
            else
            {
                globalStopwatch.Stop();
                _log.Info("Request For Edge Summary Failed. " +
                          "No valid legs. " +
                          "Symbol: " + symbol + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Mins: " + mins + ", " +
                          "Price: " + price + ", " +
                          "Percent: " + percentFromUnder + ".");
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsRequestEdgeSummaryRawAsync(string symbol, double mins, double price = double.NaN, double percentFromUnder = 1)
        {
            _log.Info("Start Request For Edge Summary. " +
                      "Symbol: " + symbol + ", " +
                      "Mins: " + mins + ", " +
                      "Price: " + price + ", " +
                      "Percent: " + percentFromUnder + ".");
            Stopwatch globalStopwatch = Stopwatch.StartNew();
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount > 0)
            {
                string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                if (underlyingDetails == null)
                {
                    return "Loading underlying info failed. Symbol: " + underlyingSymbol;
                }

                double midPrice;
                if (!double.IsNaN(price) && price > 0)
                {
                    midPrice = price;
                }
                else
                {
                    double bid = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Bid);
                    double ask = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Ask);
                    midPrice = (bid + ask) / 2;

                    _log.Info("Price Calculated For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Mins: " + mins + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");
                }

                Dictionary<DateTime, DataPointModel> snapTimeToDataPointMap = new();
                DateTime targetStartDay = DateTime.Today;
                switch (targetStartDay.DayOfWeek)
                {
                    case DayOfWeek.Saturday:
                        targetStartDay -= TimeSpan.FromDays(1);
                        break;
                    case DayOfWeek.Sunday:
                        targetStartDay -= TimeSpan.FromDays(2);
                        break;
                }

                DateTime marketClose = targetStartDay.Date + TimeSpan.FromHours(15);
                if (underlyingSymbol.StartsWith("$"))
                {
                    marketClose += TimeSpan.FromMinutes(15);
                }

                DateTime endDateTime = marketClose > DateTime.Now ? DateTime.Now : marketClose;
                DateTime startDateTime = endDateTime - TimeSpan.FromMinutes(mins);

                _log.Info("Time Range Selected For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + midPrice + ", " +
                          "Percent: " + percentFromUnder + ".");

                int totalSamples = 0;
                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);

                    if (!leg.valid)
                    {
                        _log.Info("Invalid Leg For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                  "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "Price: " + midPrice + ", " +
                                  "Percent: " + percentFromUnder + ".");

                        continue;
                    }

                    int ratio = leg.buySell ? leg.ratio : -leg.ratio;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    List<Models.Data.Responses.OptionSnapshot> results = OmsCore.EdgeScannerClient.RequestOptionSnapshots(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    stopwatch.Stop();
                    _log.Info("Response Received For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                              "Snapshots Found: " + results?.Count + ", " +
                              "Elapsed: " + stopwatch.ElapsedMilliseconds + "ms, " +
                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");

                    if (results != null)
                    {
                        totalSamples = Math.Max(totalSamples, results.Count);

                        for (int j = 0; j < results.Count; j++)
                        {
                            Models.Data.Responses.OptionSnapshot result = results[j];
                            _log.Info("Result Snapshot For Edge Summary. " +
                                      "Symbol: " + symbol + ", " +
                                      "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                      "Update: " + (j + 1) + ", " +
                                      "Bid: " + result.Bid + ", " +
                                      "Ask: " + result.Ask + ", " +
                                      "UnderBid: " + result.UnderBid + ", " +
                                      "UnderAsk: " + result.UnderAsk + ", " +
                                      "AdjTheo: " + result.AdjTheo + ", " +
                                      "Theo: " + result.Theo + ", " +
                                      "Delta: " + result.Delta + ", " +
                                      "Vega: " + result.Vega + ", " +
                                      "Iv: " + result.Iv + ", " +
                                      "QuoteTime: " + result.QuoteTime + ", " +
                                      "SnapshotTime: " + result.SnapshotTime + ", " +
                                      "HanweckCalcTime: " + result.HanweckCalcTime + ", " +
                                      "AdjTheoTime: " + result.AdjTheoTime + ", " +
                                      "UnderMid: " + result.UnderMid + ", " +
                                      "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ".");


                            double resultMid = (result.UnderAsk + result.UnderBid) / 2;
                            double percentageDifference = Math.Abs((midPrice - resultMid) / ((midPrice + resultMid) / 2));
                            if (percentageDifference > percentFromUnder)
                            {
                                _log.Info("Skipping Result Snapshot By Price For Edge Summary. " +
                                          "Symbol: " + symbol + ", " +
                                          "Leg-" + (i + 1) + ": " + leg.symbol + ", " +
                                          "Snapshot Mid: " + resultMid + ", " +
                                          "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                          "Price: " + midPrice + ", " +
                                          "Percent: " + percentFromUnder + ".");
                                continue;
                            }

                            if (!snapTimeToDataPointMap.TryGetValue(result.SnapshotTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapshotTime,
                                };
                                snapTimeToDataPointMap[result.SnapshotTime] = dataPoint;
                            }

                            dataPoint.UnderPx = resultMid;

                            double bidPrice = result.Bid;
                            bidPrice += (midPrice - resultMid) * result.Delta;

                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                            double hwTheo = result.Theo;
                            hwTheo += (midPrice - resultMid) * result.Delta;
                            dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwTheo, result);

                            double askPrice = result.Ask;
                            askPrice += (midPrice - resultMid) * result.Delta;

                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                        }
                    }
                }
                List<DataPointModel> dataPoints = snapTimeToDataPointMap.Values
                    .Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount))
                    .OrderByDescending(x => x.Timestamp)
                    .ToList();

                string output;

                DataPointModel highestBid = dataPoints.MaxBy(x => x.BidIv);
                DataPointModel lowestAsk = dataPoints.MinBy(x => x.AskIv);
                DataPointModel tightestBidToTheo = dataPoints.MinBy(x => Math.Abs(x.BidIv - x.Iv));
                DataPointModel tightestAskToTheo = dataPoints.MinBy(x => Math.Abs(x.AskIv - x.Iv));

                output = "HighestBid," + (highestBid != null ? Math.Round(highestBid.BidIv, 3) : "") +
                         ";LowestOffer," + (lowestAsk != null ? Math.Round(lowestAsk.AskIv, 3) : "") +
                         ";TightestBidToTheo," + (tightestBidToTheo != null ? Math.Round(Math.Abs(tightestBidToTheo.BidIv - tightestBidToTheo.Iv), 3) : "") +
                         ";TightestAskToTheo," + (tightestAskToTheo != null ? Math.Round(Math.Abs(tightestAskToTheo.AskIv - tightestAskToTheo.Iv), 3) : "") +
                         ";Underlying," + Math.Round(midPrice, 3) +
                         ";TotalSamples," + totalSamples +
                         ";SelectedSamples," + dataPoints.Count;


                globalStopwatch.Stop();
                _log.Info("Valid Datapoints For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Datapoints: " + dataPoints.Count + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                          "Price: " + Math.Round(midPrice, 2) + ", " +
                          "Percent: " + percentFromUnder + ", " +
                          "Output: " + output + ".");

                return output;
            }
            else
            {
                globalStopwatch.Stop();
                _log.Info("Request For Edge Summary Failed. " +
                          "No valid legs. " +
                          "Symbol: " + symbol + ", " +
                          "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                          "Mins: " + mins + ", " +
                          "Price: " + price + ", " +
                          "Percent: " + percentFromUnder + ".");
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }

        private static object OmsRequestRecalculatedIvChainAsync(string symbol, double days, double price = double.NaN)
        {
            SymbolCodec symbolCodec = new(symbol);
            if (symbolCodec.LegCount <= 0)
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
            else
            {
                List<DataPointModel> dataPoints = GetRecalculatedIvDataPoints(days, price, symbolCodec);
                string value = ";" + string.Join(";", dataPoints.Select(x => x.Timestamp.ToString("hh:mm:ss") + "," + x.BidIv + "," + x.Iv + "," + x.AskIv + "," + x.UnderPx));
                return value;
            }
        }

        private static List<DataPointModel> GetRecalculatedIvDataPoints(double days, double price, SymbolCodec symbolCodec)
        {
            string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "").Replace(".", "");
            MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
            if (underlyingDetails == null)
            {
                throw new SlimException("Loading underlying info failed");
            }

            double midPrice;
            if (!double.IsNaN(price))
            {
                midPrice = price;
            }
            else
            {
                double bid = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Bid);
                double ask = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Ask);
                midPrice = (bid + ask) / 2;
            }

            DateTime lastUpdateTime = DateTime.Now;

            Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();

            days = Math.Min(days, MAX_REQUEST_DAYS);
            TimeSpan span = TimeSpan.FromDays(days);
            DateTime endDateTime = DateTime.Now;
            DateTime startDateTime = endDateTime - span;
            int weekend = TimeHelper.GetWeekendDaysBetween(startDateTime, endDateTime);
            if (weekend > 0)
            {
                startDateTime -= TimeSpan.FromDays(weekend);
            }

            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);
                int ratio = leg.buySell ? leg.ratio : -leg.ratio;

                if (!leg.valid)
                {
                    continue;
                }

                Task<List<OptionSnapshot>> snapshotsTask = OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                snapshotsTask.Wait();
                List<OptionSnapshot> results = snapshotsTask.Result;

                if (results != null)
                {
                    double bid = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Bid);
                    double ask = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Ask);
                    double iv = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.ImpliedVol);
                    double vega = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Vega);

                    results.Add(new OptionSnapshot()
                    {
                        SnapTime = lastUpdateTime,
                        UnderAsk1 = midPrice,
                        UnderBid1 = midPrice,
                        Bid = bid,
                        Ask = ask,
                        HwIV = iv,
                        HwVega = vega,
                    });

                    foreach (OptionSnapshot result in results)
                    {
                        if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                        {
                            dataPoint = new DataPointModel()
                            {
                                Timestamp = result.SnapTime,
                            };
                            snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                        }
                        double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                        PricingParameters pricingParameters = new()
                        {
                            Volatility = 0.0,
                            PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                            Strike = leg.strike,
                            DaysToExpiration = (leg.expiration - result.SnapTime).TotalDays,
                            RiskFreeRate = underlyingDetails.RiskFreeRate,
                            StockRate = underlyingDetails.StockRate,
                            UnderlyingPrice = resultMid,
                            UnderlyingMultiplier = underlyingDetails.Multiplier,
                            ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                        };
                        pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapTime);

                        Greeks greeks = new();
                        dataPoint.UnderPx = resultMid;

                        double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                        dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidIv, result);

                        double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.HwTV, greeks);
                        dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwIv, result);

                        double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                        dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askIv, result);
                    }
                }
            }

            List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values
                .Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount))
                .OrderByDescending(x => x.Timestamp)
                .ToList();
            return dataPoints;
        }

        private static object OmsRequestRecalculatedIvPriceChainAsync(string symbol, double days, double price = double.NaN)
        {
            Stopwatch time = Stopwatch.StartNew();
            try
            {
                SymbolCodec symbolCodec = new(symbol);
                if (symbolCodec.LegCount > 0)
                {
                    string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
                    MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(underlyingSymbol);
                    if (underlyingDetails == null)
                    {
                        return "Loading underlying info failed. Symbol: " + underlyingSymbol;
                    }

                    double midPrice;
                    if (!double.IsNaN(price))
                    {
                        midPrice = price;
                    }
                    else
                    {
                        double bid = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Bid);
                        double ask = OmsCore.QuoteClient.GetQuoteSnapshot(underlyingSymbol, SubscriptionFieldType.Ask);
                        midPrice = (bid + ask) / 2;
                    }

                    DateTime lastUpdateTime = DateTime.Now;

                    Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();

                    days = Math.Min(days, MAX_REQUEST_DAYS);
                    TimeSpan span = TimeSpan.FromDays(days);
                    DateTime endDateTime = DateTime.Now;
                    DateTime startDateTime = endDateTime - span;
                    int weekend = TimeHelper.GetWeekendDaysBetween(startDateTime, endDateTime);
                    if (weekend > 0)
                    {
                        startDateTime -= TimeSpan.FromDays(weekend);
                    }
                    for (int i = 0; i < symbolCodec.LegCount; i++)
                    {
                        Instrument leg = symbolCodec.GetLeg(i);
                        int ratio = leg.buySell ? leg.ratio : -leg.ratio;

                        if (!leg.valid)
                        {
                            continue;
                        }

                        Task<List<OptionSnapshot>> snapshotsTask = OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                        snapshotsTask.Wait();
                        List<OptionSnapshot> results = snapshotsTask.Result;

                        if (results != null)
                        {
                            double bid = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Bid);
                            double ask = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Ask);
                            double iv = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.ImpliedVol);
                            double vega = OmsCore.QuoteClient.GetQuoteSnapshot(leg.symbol, SubscriptionFieldType.Vega);

                            results.Add(new OptionSnapshot()
                            {
                                SnapTime = lastUpdateTime,
                                UnderAsk1 = midPrice,
                                UnderBid1 = midPrice,
                                Bid = bid,
                                Ask = ask,
                                HwIV = iv,
                                HwVega = vega,
                            });
                            foreach (OptionSnapshot result in results)
                            {
                                if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                                {
                                    dataPoint = new DataPointModel()
                                    {
                                        Timestamp = result.SnapTime,
                                    };
                                    snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                                }
                                double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                                PricingParameters pricingParameters = new()
                                {
                                    Volatility = 0.0,
                                    PutCall = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                    Strike = leg.strike,
                                    DaysToExpiration = (leg.expiration - result.SnapTime).TotalDays,
                                    RiskFreeRate = underlyingDetails.RiskFreeRate,
                                    StockRate = underlyingDetails.StockRate,
                                    UnderlyingPrice = resultMid,
                                    UnderlyingMultiplier = underlyingDetails.Multiplier,
                                    ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                                };
                                pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapTime);
                                Greeks greeks = new();
                                dataPoint.UnderPx = resultMid;

                                double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                                pricingParameters.Volatility = bidIv;
                                double bidPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                                bidPrice += (midPrice - resultMid) * greeks.Delta;
                                dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                                double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.HwTV, greeks);
                                pricingParameters.Volatility = hwIv;
                                double hwTheo = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                                hwTheo += (midPrice - resultMid) * greeks.Delta;
                                dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwTheo, result);

                                double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                                pricingParameters.Volatility = askIv;
                                double askPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                                askPrice += (midPrice - resultMid) * greeks.Delta;
                                dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                            }
                        }
                    }

                    List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values
                        .Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount))
                        .OrderByDescending(x => x.Timestamp)
                        .ToList();

                    string returnValue = ";" + string.Join(";", dataPoints.Select(x => x.Timestamp.ToString("hh:mm:ss") + "," + x.BidIv + "," + x.Iv + "," + x.AskIv + "," + x.UnderPx));

                    string message = $"Price chain request complete. Symbol: {symbol}, Days: {days}, Took: {time.ElapsedMilliseconds}MS.";
                    _log.Info(message);

                    return returnValue;
                }
                else
                {
                    return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
                }
            }
            finally
            {
                time.Stop();
            }
        }

        private static object RequestMatchingHanweckUpdatesAsync(string symbols)
        {
            List<string> symbolsList = symbols.Split(",").Select(x => x.Trim()).ToList();

            if (symbolsList.Count > 0)
            {
                Task<Models.Data.Responses.HanweckUpdatesWithMatchingTimestampsResponse> responseTask = OmsCore.UpdateManager.RequestHanweckUpdatesWithMatchingTimestampsAsync(symbolsList);
                responseTask.Wait();
                Models.Data.Responses.HanweckUpdatesWithMatchingTimestampsResponse response = responseTask.Result;
                if (response.UpdateFound)
                {

                    string returnString = response.Price + ";";
                    foreach (KeyValuePair<string, double> kvp in response.SymbolToTheoMap)
                    {
                        returnString += kvp.Key + "," + kvp.Value + ";";
                    }
                    returnString += response.Timestamp.ToString("hh:mm:ss.ffff tt");

                    return returnString;
                }
                else
                {
                    return "Matching update not found.";
                }
            }
            else
            {
                return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorValue);
            }
        }
    }
}
