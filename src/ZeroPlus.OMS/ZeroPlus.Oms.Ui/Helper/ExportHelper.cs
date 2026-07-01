using DevExpress.Spreadsheet;
using NLog;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Responses;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    internal class ExportHelper
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        internal static void WriteSpreadsToFileUsingDominatorFormat(string username, string filePath, List<SymbolFishStatusResponse> response, bool randomizeExport = false)
        {
            List<SymbolCodec> symbolCodecs = response.Select(spread => new SymbolCodec(spread.Symbol)).ToList();
            WriteSpreadsToFileUsingDominatorFormat(username, filePath, symbolCodecs, response, randomizeExport);
        }

        public static void WriteSpreadsToFileUsingDominatorFormat(string username, string filePath, List<OmsOrderModel> spreads, bool randomizeExport = false)
        {
            List<SymbolCodec> symbolCodecs = spreads.Select(spread => new SymbolCodec(spread.Symbol)).ToList();
            WriteSpreadsToFileUsingDominatorFormat(username, filePath, symbolCodecs, null, randomizeExport);
        }

        internal static void WriteSpreadsToFileUsingDominatorFormat(string username, string filePath, List<SymbolCodec> spreads, List<SymbolFishStatusResponse> fishStatus = null, bool randomizeExport = false)
        {
            try
            {
                using Workbook workbook = new();
                Worksheet worksheet = workbook.Worksheets[0];
                workbook.BeginUpdate();
                try
                {
                    int rowsCount = Math.Max(2, spreads.Count);
                    object[,] values = new object[rowsCount, 24];

                    values[0, 21] = DateTime.Today.ToString("M.d.yy");
                    values[1, 21] = username;
                    Dictionary<string, double> underlyingSymbolToLastPriceMap = new();
                    int index = -1;
                    for (int i = 0; i < spreads.Count; i++)
                    {
                        SymbolCodec spread = spreads[i];
                        try
                        {
                            if (spread.LegCount == 0)
                            {
                                continue;
                            }

                            OptionsHelper.CorrectLegOrder(spread);
                            string underlying = spread.UnderlyingSymbol();
                            if (!underlyingSymbolToLastPriceMap.ContainsKey(underlying))
                            {
                                double lastPrice = OmsCore.QuoteClient.GetSnapshotAsync(underlying, SubscriptionFieldType.LastPrice).Result;
                                underlyingSymbolToLastPriceMap[underlying] = lastPrice;
                            }
                            Option leg1 = null;
                            Option leg2 = null;
                            Option leg3 = null;
                            Option leg4 = null;
                            if (spread.LegCount > 0)
                            {
                                Instrument leg = spread.GetLeg(0);
                                if (!leg.symbol.StartsWith('.'))
                                {
                                    continue;
                                }
                                leg1 = OptionsHelper.GetOptionFromSymbol(leg.symbol);
                            }
                            if (spread.LegCount > 1)
                            {
                                Instrument leg = spread.GetLeg(1);
                                if (!leg.symbol.StartsWith('.'))
                                {
                                    continue;
                                }
                                leg2 = OptionsHelper.GetOptionFromSymbol(leg.symbol);
                            }
                            if (spread.LegCount > 2)
                            {
                                Instrument leg = spread.GetLeg(2);
                                if (!leg.symbol.StartsWith('.'))
                                {
                                    continue;
                                }
                                leg3 = OptionsHelper.GetOptionFromSymbol(leg.symbol);
                            }
                            if (spread.LegCount > 3)
                            {
                                Instrument leg = spread.GetLeg(3);
                                if (!leg.symbol.StartsWith('.'))
                                {
                                    continue;
                                }
                                leg4 = OptionsHelper.GetOptionFromSymbol(leg.symbol);
                            }

                            index++;
                            values[index, 0] = TimeHelper.IsThirdFridayOfTheMonth(leg1.Expiration) &&
                                leg1.UnderlyingSymbol?.Replace("$", string.Empty) == leg1.RootSymbol
                                ? leg1.Expiration.ToString("MMM yy").ToUpper()
                                : (object)leg1.Expiration.ToString("dd_MMM_yy").ToUpper();
                            string spreadTos = spread.ToTOS();
                            if (OptionStrategy.TryIdentify(spreadTos, out BaseStrategy baseStrategy, out _, out _))
                            {
                                values[0, 22] = baseStrategy.ToString().Replace("_", " ");
                                switch (baseStrategy)
                                {
                                    case BaseStrategy.CALL_VERTICAL:
                                    case BaseStrategy.PUT_VERTICAL:
                                    case BaseStrategy.CALL_1X2:
                                    case BaseStrategy.PUT_1X2:
                                    case BaseStrategy.CALL_1X3:
                                    case BaseStrategy.PUT_1X3:
                                    case BaseStrategy.CALL_2X3:
                                    case BaseStrategy.PUT_2X3:
                                    case BaseStrategy.CALL_BUTTERFLY:
                                    case BaseStrategy.PUT_BUTTERFLY:
                                    case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                                    case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                                    case BaseStrategy.IRON_BUTTERFLY:
                                    case BaseStrategy.CALL_CONDOR:
                                    case BaseStrategy.PUT_CONDOR:
                                    case BaseStrategy.IRON_CONDOR:
                                    case BaseStrategy.CALL_1X3X3X1:
                                    case BaseStrategy.PUT_1X3X3X1:
                                        values[index, 1] = leg1.Strike;
                                        values[index, 2] = leg2.Strike;
                                        if (leg3 != null)
                                        {
                                            values[index, 12] = leg3.Strike;
                                        }
                                        break;
                                    case BaseStrategy.CALL_CALENDAR:
                                    case BaseStrategy.PUT_CALENDAR:
                                    case BaseStrategy.CALL_DIAGONAL:
                                    case BaseStrategy.PUT_DIAGONAL:
                                        values[index, 1] = TimeHelper.IsThirdFridayOfTheMonth(leg2.Expiration) &&
                                            leg2.UnderlyingSymbol?.Replace("$", string.Empty) == leg2.RootSymbol
                                            ? leg2.Expiration.ToString("MMM yy").ToUpper()
                                            : (object)leg2.Expiration.ToString("dd_MMM_yy").ToUpper();

                                        values[index, 2] = leg1.Strike;
                                        values[index, 12] = leg2.Strike;
                                        break;
                                }

                                switch (baseStrategy)
                                {
                                    case BaseStrategy.CALL_VERTICAL:
                                    case BaseStrategy.PUT_VERTICAL:
                                        values[index, 4] = "1X1";
                                        break;
                                    case BaseStrategy.CALL_1X2:
                                    case BaseStrategy.PUT_1X2:
                                        values[index, 4] = "1X2";
                                        break;
                                    case BaseStrategy.CALL_1X3:
                                    case BaseStrategy.PUT_1X3:
                                        values[index, 4] = "1X3";
                                        break;
                                    case BaseStrategy.CALL_BUTTERFLY:
                                    case BaseStrategy.PUT_BUTTERFLY:
                                    case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                                    case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                                    case BaseStrategy.IRON_BUTTERFLY:
                                        values[index, 4] = "FLY";
                                        break;
                                }

                                values[index, 3] = leg1.Type == OptionType.CALL ? "Call" : "Put";
                                values[index, 7] = spreadTos;
                                values[index, 14] = leg1?.OptionSymbol;
                                values[index, 15] = leg2?.OptionSymbol;
                                values[index, 16] = leg3?.OptionSymbol;
                                values[index, 17] = leg4?.OptionSymbol;
                                values[index, 18] = spread.UnderlyingSymbol()?.Replace("$", string.Empty);
                                values[index, 23] = 1;

                                try
                                {
                                    if (fishStatus != null)
                                    {
                                        SymbolFishStatusResponse status = fishStatus[i];
                                        if (status != null)
                                        {
                                            values[index, 24] = status.FishStatus;
                                            values[index, 25] = status.FishLevel;
                                            values[index, 26] = status.FishEdge;
                                            values[index, 27] = status.FishLevelSell;
                                            values[index, 28] = status.FishEdgeSell;
                                            values[index, 29] = status.LastFishTime;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    throw;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, nameof(WriteSpreadsToFileUsingDominatorFormat));
                        }
                    }

                    values[0, 20] = string.Join(";", underlyingSymbolToLastPriceMap.Keys.Select(x => x.Replace("$", string.Empty)));
                    values[1, 22] = string.Join(";", underlyingSymbolToLastPriceMap.Select(x => x.Key + "," + x.Value));

                    if (randomizeExport)
                    {
                        Random random = new();
                        int row = values.GetLength(0);
                        int columns = values.GetLength(1);

                        // Dont change metadata columns
                        if (columns > 4)
                        {
                            columns -= 4;
                        }

                        while (row > 1)
                        {
                            int swapRow = random.Next(row--);
                            for (int col = 0; col < columns; col++)
                            {
                                (values[swapRow, col], values[row, col]) = (values[row, col], values[swapRow, col]);
                            }
                        }
                    }

                    for (int row = 0; row < values.GetLength(0); row++)
                    {
                        for (int col = 0; col < values.GetLength(1); col++)
                        {
                            object value = values[row, col];
                            worksheet.Cells[row, col].SetValue(value);
                        }
                    }
                }
                finally
                {
                    workbook.EndUpdate();
                }

                workbook.SaveDocument(filePath, DocumentFormat.Xlsx);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(WriteSpreadsToFileUsingDominatorFormat));
            }
        }
    }
}