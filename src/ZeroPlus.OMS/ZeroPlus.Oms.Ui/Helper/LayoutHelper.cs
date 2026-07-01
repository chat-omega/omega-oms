using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;

namespace ZeroPlus.Oms.Ui.Helper
{
    internal class LayoutHelper
    {
        internal static HashSet<string> BasketDeadColumns { get; } =
        [
            "VolaTheo",
            "VolaTheoAdj",
            "VolaPriceMetric",
        ];

        internal static HashSet<string> OrderbookDeadColumns { get; } =
        [
            "Currency",
            "Destination",
            "ExchangeOrderID",
            "ExecutionType",
            "Guid",
            "LocalID",
            "Reason",
            "Request",
            "AccountAcronym",
            "RequestSymbol",
            "RouteOverride",
            "Security",
            "SecurityBook",
            "SmartRoute",
            "Fee2",
            "Multiplier",
            "TagFirmVolume",
            "TagVolume",
            "TV",
            "DeltaAdjBestBuyPrice",
            "DeltaAdjBestSellPrice",
            "AdjAveragePrice",
            "AdjustedEdgeOverride",
            "AveragePriceDiff",
            "LiveUnderMid",
            "Ema",
            "EdgeCurveAdjustment",
            "EdgeOverride",
            "Last",
            "LoadingDetails",

            "TransactionID",
            "ExchangeOrderID",
            "ExecutingBroker",
            "ExecutionID",
            "ExecutionReferenceID",
            "OriginalOrderID",
            "ClearingID",
            "LocalID",
            "AccountID",
            "RoutingSession",
            "ClearingFirm",
            "OmsBestBidPercent",
            "CloseTV",
            "CloseDelta",
            "CloseTotalDelta",
            "CloseHanweckTotalTheo",
            "CloseHanweckTotalGamma",
            "CloseHanweckTotalVega",
            "CloseHanweckTotalTheta",
            "CloseHanweckTotalRho",
            "CloseHanweckTotalIV",
            "CloseBid",
            "CloseAsk",
            "CloseUnderBid",
            "CloseUnderAsk",
            "CloseHanweckTotalUnder",
            "CloseHanweckTotalUBid",
            "CloseHanweckTotalUAsk",
            "CloseHanweckTotalBid",
            "CloseHanweckTotalAsk",
            "CloseDeltaAdjustedTheo",
            "CloseUnderlyingBidSize",
            "CloseUnderlyingAskSize",
            "CloseBidSize",
            "CloseAskSize",
            "CloseBidPercentOfFillPrice",

            "HanweckTimestampLeg1",
            "HanweckTimestampLeg2",
            "HanweckTimestampLeg3",
            "HanweckTimestampLeg4",
            "HanweckTimestampsMatch",
            "HanweckTheoLeg1",
            "HanweckTheoLeg2",
            "HanweckTheoLeg3",
            "HanweckTheoLeg4",

            "DateAdded",
            "Timestamp",
            "TagMktMkrBid",
            "TagMktMkrAsk",
            "MinPrice",
            "MaxPrice",
            "IsAutomation",
            "IsTagged",
            "IsComplexOrder",
            "OmsBestBidPercent",
            "FullTag",
            "Trader",
            "SpreadAvgPrice",
            "Strike",
            "StrikeSpacing",
            "ExpirationSpacing",
            "AllExchanges",

            "SkipNewPriceEvaluation",
            "Subtype"
        ];

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        protected static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        internal static void RestoreLayoutOfControl(string id, GridControl control)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string controlName = control.Name;
                string layoutExportPath = Path.Combine(layoutDir, $"{id}-{controlName}-layout.xml");
                bool success = TryRestoreLayout(control, layoutExportPath);

                if (!success)
                {
                    layoutExportPath = Path.Combine(layoutDir, $"{controlName}-layout.xml");
                    TryRestoreLayout(control, layoutExportPath);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreLayoutOfControl));
            }
        }

        internal static void RestoreLayoutOfControl(string id, ColumnDefinition gridSplitterCol)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string controlName = gridSplitterCol.Name;
                string layoutExportPath = Path.Combine(layoutDir, $"{id}-{controlName}-layout.xml");
                if (File.Exists(layoutExportPath))
                {
                    string val = File.ReadAllText(layoutExportPath);
                    GridLengthConverter glc = new();
                    gridSplitterCol.Width = (GridLength)glc.ConvertFromString(val)!;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreLayoutOfControl));
            }
        }

        internal static string GetLayoutAsString(GridControl view)
        {
            try
            {
                DXSerializer.SetStoreLayoutMode(view, StoreLayoutMode.All);

                using MemoryStream stream = new();
                view.SaveLayoutToStream(stream);
                stream.Seek(0, SeekOrigin.Begin);
                using StreamReader reader = new(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetLayoutAsString));
                return String.Empty;
            }
        }

        internal static void RestoreLayoutFromString(string contents, GridControl grid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(contents))
                {
                    return;
                }
                if (OmsCore.Config.RemoveDuplicateColumnsFromLayout)
                {
                    contents = RemoveHiddenAndDuplicateColumns(contents);
                }
                byte[] buffer = Encoding.UTF8.GetBytes(contents);
                RestoreLayoutFromBuffer(buffer, grid);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreLayoutFromString));
            }
        }

        internal static void RestoreLayoutFromBuffer(byte[] buffer, GridControl grid)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                using MemoryStream stream = new(buffer);
                grid.RestoreLayoutFromStream(stream);
                _log.Info($"Layout Restored. Name: {grid.Name}, Elapsed: {stopwatch.ElapsedMilliseconds}, Buffer Len: {buffer.Length}");
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public static string RemoveHiddenAndDuplicateColumns(string content, HashSet<string> eliminationList = null)
        {
            try
            {
                XDocument doc = XDocument.Parse(content);
                Dictionary<string, XElement> takenNames = new();
                HashSet<XElement> elementsToRemove = new();
                if (doc.Root == null)
                {
                    return content;
                }
                foreach (var item in doc.Root.Elements())
                {
                    if (item.HasAttributes)
                    {
                        var name = item.Attribute("name");
                        if (name is { Value: "$GridControl" })
                        {
                            foreach (var gridControlItems in item.Elements())
                            {
                                if (gridControlItems.HasAttributes)
                                {
                                    name = gridControlItems.Attribute("name");
                                    if (name is { Value: "Columns" })
                                    {
                                        foreach (var columnItem in gridControlItems.Elements())
                                        {
                                            if (columnItem.HasAttributes)
                                            {
                                                var isKeyAt = columnItem.Attribute("iskey");
                                                if (isKeyAt is { Value: "true" })
                                                {
                                                    string columnName = string.Empty;
                                                    bool visibilityColFound = false;
                                                    foreach (var prop in columnItem.Elements())
                                                    {
                                                        if (prop.HasAttributes)
                                                        {
                                                            var property = prop.Attribute("name");
                                                            if (property != null)
                                                            {
                                                                var propValue = prop.Value;
                                                                if (!string.IsNullOrWhiteSpace(propValue) && string.IsNullOrWhiteSpace(columnName) && property.Value == "Header")
                                                                {
                                                                    columnName = propValue.Replace(" ", "");
                                                                }
                                                                else if (!string.IsNullOrWhiteSpace(propValue) && property.Value == "Name")
                                                                {
                                                                    columnName = propValue;
                                                                }
                                                                else if (eliminationList != null && !string.IsNullOrWhiteSpace(propValue) && (property.Value == "FieldName" && eliminationList.Contains(propValue)) || (!string.IsNullOrWhiteSpace(columnName) && property.Value == "Header" && eliminationList.Contains(columnName.Replace(" ", ""))))
                                                                {
                                                                    elementsToRemove.Add(columnItem);
                                                                    break;
                                                                }
                                                                else if (property.Value == "AllowUnboundExpressionEditor")
                                                                {
                                                                    elementsToRemove.Add(columnItem);
                                                                    break;
                                                                }
                                                                else if (property.Value == "Visible")
                                                                {
                                                                    visibilityColFound = true;
                                                                    if (propValue == "false")
                                                                    {
                                                                        elementsToRemove.Add(columnItem);
                                                                    }
                                                                    else if (propValue == "true")
                                                                    {
                                                                        if (!string.IsNullOrWhiteSpace(columnName))
                                                                        {
                                                                            if (takenNames.TryGetValue(columnName, out var prevElement))
                                                                            {
                                                                                elementsToRemove.Add(prevElement);
                                                                            }
                                                                            takenNames[columnName] = columnItem;
                                                                        }
                                                                        else
                                                                        {
                                                                            elementsToRemove.Add(columnItem);
                                                                        }
                                                                    }
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }

                                                    if (!visibilityColFound)
                                                    {
                                                        elementsToRemove.Add(columnItem);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var element in elementsToRemove)
                {
                    element.Remove();
                }

                content = doc.ToString();
                return content;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveHiddenAndDuplicateColumns));
                return content;
            }
        }

        internal static void RestoreLayoutOfControl(string id, BarManager control)
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string controlName = control.Name;
                string layoutExportPath = Path.Combine(layoutDir, $"{id}-{controlName}-layout.xml");
                if (!string.IsNullOrWhiteSpace(layoutExportPath) && File.Exists(layoutExportPath))
                {
                    control.RestoreLayoutFromXml(layoutExportPath);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RestoreLayoutOfControl));
            }
        }

        private static bool TryRestoreLayout(GridControl control, string layoutExportPath)
        {
            if (!string.IsNullOrWhiteSpace(layoutExportPath) && File.Exists(layoutExportPath))
            {
                control.RestoreLayoutFromXml(layoutExportPath);
                return true;
            }
            return false;
        }

    }
}
