using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class DominatorListRollerViewModel : CustomizableTableViewModelBase, ISpreadsGenerator
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private List<string> _spreads;
        private List<SymbolCodec> _latestSpreads;
        private CancellationTokenSource _cancellationTokenSource;
        private Dictionary<string, double> _underlyingToCreationLastPriceMap;


        protected ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial ObservableCollection<SpreadGeneratorResults> LatestSpreadGeneratorResults { get; set; }

        [Bindable]
        public partial double ParserFrontMonthPercent { get; set; }

        [Bindable]
        public partial double ParserBackMonthPercent { get; set; }

        [Bindable]
        public partial int ParserMinCount { get; set; }

        [Bindable]
        public partial int ParserGroupTotalCount { get; set; }

        [Bindable]
        public partial string InputListPath { get; set; }

        [Bindable]
        public partial string InputListName { get; set; }

        [Bindable]
        public partial string Underlying { get; set; }

        [Bindable]
        public partial bool ShowProgressBar { get; set; }

        [Bindable]
        public partial double ProgressValue { get; set; }

        [Bindable]
        public partial string ProgressStatus { get; set; }

        [Bindable]
        public partial DateTime? CreationDate { get; set; }

        [Bindable]
        public partial double MaxDte { get; set; }

        [Bindable]
        public partial double DatesToRoll { get; set; }

        [Bindable]
        public partial double SavedLastPrice { get; set; }

        [Bindable]
        public partial double ChangeInLastPrice { get; set; }

        [Bindable]
        public partial bool CanExport { get; set; }

        [Bindable]
        public partial bool FillInMissingExpirations { get; set; }
        [Bindable]
        public partial int RollRange { get; set; }

        public DominatorListRollerViewModel()
        {
            _spreads = new List<string>();
            _latestSpreads = new List<SymbolCodec>();
            _underlyingToCreationLastPriceMap = new Dictionary<string, double>();
            _cancellationTokenSource = new CancellationTokenSource();
            FillInMissingExpirations = true;
            LatestSpreadGeneratorResults = new ObservableCollection<SpreadGeneratorResults>();
            RollRange = 3;
            CreationDate = null;
            ModuleTitle = "Dominator List Roller";
        }

        [Command]
        public async Task BrowseFilesAsyncCommand()
        {
            try
            {
                ShowProgressBar = true;
                ProgressValue = 0;
                CreationDate = null;
                InputListPath = string.Empty;
                InputListName = string.Empty;
                DatesToRoll = 0;
                SavedLastPrice = 0;
                ChangeInLastPrice = 0;
                CanExport = false;
                Progress<double> progressIndicator = new(ProgressUpdate);
                _cancellationTokenSource = new CancellationTokenSource();

                OpenFileDialogService.Filter = "Excel files|*.XLS*";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    ProgressStatus = "Loading input file.";
                    IFileInfo file = OpenFileDialogService.Files.First();
                    InputListPath = file.GetFullName();
                    InputListName = file.Name;
                    await ExcelHelper.ReadExcelFileAsync(InputListPath, _cancellationTokenSource.Token, progressIndicator)
                        .ContinueWith(async t => await LoadSpreadsFromExcelDumpAsync(t.Result));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseFilesAsyncCommand));
            }
            finally
            {
                ProgressValue = 0;
            }
        }

        [Command]
        public async Task RollListAsyncCommand()
        {
            try
            {
                if (_spreads.Count > 0)
                {
                    ShowProgressBar = true;
                    ProgressStatus = "Rolling List.";
                    _cancellationTokenSource = new CancellationTokenSource();
                    List<SymbolCodec> spreads = FillInMissingExpirations ? await RollSpreadsAndFillExpirations() : await RollSpreads();
                    _latestSpreads = spreads.DistinctBy(x => x.ToTOS()).ToList();
                    ProgressStatus = _latestSpreads.Count + " spreads rolled.";
                    ConvertToSpreadGeneratorResults(_latestSpreads);
                    CanExport = _latestSpreads.Count > 0;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RollListAsyncCommand));
                MessageBoxService.ShowMessage(ex.Message, "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error);
                ProgressStatus = _latestSpreads.Count + " spreads rolled.";
                CanExport = _latestSpreads.Count > 0;
            }
        }

        [Command]
        public void ParseSpreadsCommand()
        {
            try
            {
                ParseSpreadsConfigView parseSpreadsConfigView = new()
                {
                    DataContext = this
                };
                ParserFrontMonthPercent = 1;
                ParserBackMonthPercent = 1;
                ParserMinCount = 20;
                ParserGroupTotalCount = LatestSpreadGeneratorResults.Max(x => x.Spreads.Count);
                parseSpreadsConfigView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ParseSpreadsCommand));
                MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error);
            }
        }

        [Command]
        public void ExpirationPercentChangedCommand(SpreadGeneratorResults spreadGeneratorResults)
        {
            try
            {
                spreadGeneratorResults?.UpdateExpirationPercentage();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationPercentChangedCommand));
            }
        }

        [Command]
        public void ParserFrontMonthPercentChangedCommand()
        {
            foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
            {
                spreadGeneratorResult.FrontMonthExpirationsPercent = ParserFrontMonthPercent;
            }
        }

        [Command]
        public void ParserBackMonthPercentChangedCommand()
        {
            foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
            {
                spreadGeneratorResult.BackMonthExpirationsPercent = ParserBackMonthPercent;
            }
        }

        [Command]
        public void ParserMinCountChangedCommand()
        {
            foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
            {
                spreadGeneratorResult.MinCount = ParserMinCount;
            }
        }

        [Command]
        public void ParserGroupTotalCountChangedCommand()
        {
            foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
            {
                spreadGeneratorResult.TotalCount = ParserGroupTotalCount;
            }
        }

        [Command]
        public void ShowParseConfigCommand(SpreadGeneratorResults spreadGeneratorResults)
        {
            SpreadGeneratorResultParserInputView parseSpreadsConfigView = new();
            if (parseSpreadsConfigView.DataContext is SpreadGeneratorResultParserInputViewModel viewModel)
            {
                viewModel.SpreadGeneratorResults = spreadGeneratorResults;
                viewModel.SpreadsGenerator = this;
                parseSpreadsConfigView.ShowDialog();
            }
        }

        [Command]
        public async Task RunParserCommand()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ShowProgressBar = true;
                ProgressStatus = $"Parsing Output.";
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;
                List<Task> tasks = new();
                foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
                {
                    tasks.Add(Task.Run(() => spreadGeneratorResult.ParseToTarget(token)));
                }
                await Task.WhenAll(tasks);
                int totalCount = UpdateList();
                stopwatch.Stop();
                ShowProgressBar = false;
                ProgressStatus = $"Done! {totalCount:N0} spreads parsed, in {stopwatch.ElapsedMilliseconds}ms.";
            }
            catch (TaskCanceledException)
            {
                int totalCount = UpdateList();
                stopwatch.Stop();
                ShowProgressBar = false;
                ProgressStatus = $"Operation Cancelled! {totalCount:N0} spreads parsed, in {stopwatch.ElapsedMilliseconds}ms.";
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationPercentChangedCommand));
            }
        }

        private int UpdateList()
        {
            _latestSpreads = new List<SymbolCodec>();
            foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
            {
                foreach (Spread spread in spreadGeneratorResult.Spreads)
                {
                    _latestSpreads.Add(new SymbolCodec(spread.Symbol));
                }
            }
            return _latestSpreads.Count;
        }

        [Command]
        public void CancelParserCommand()
        {
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelParserCommand));
            }
        }

        private async Task<List<SymbolCodec>> RollSpreads()
        {
            List<SymbolCodec> spreads = new();
            Dictionary<string, double> underlyingToLastPriceMap = new();
            TimeSpan timeSpan = TimeSpan.FromDays(DatesToRoll);
            foreach (string spread in _spreads)
            {
                try
                {
                    SymbolCodec newSpread = new();
                    SymbolCodec symbolCodec = new(spread);
                    bool valid = true;
                    for (int i = 0; i < symbolCodec.LegCount; i++)
                    {
                        Instrument leg = symbolCodec.GetLeg(i);
                        string underlyingSymbol = await SwapUnderlyingAsync() ? Underlying : leg.underlyingSymbol;
                        List<Option> options = await OmsCore.QuoteClient.GetOptionsAsync(underlyingSymbol);

                        double changeInLastPrice = 0.0;
                        if (_underlyingToCreationLastPriceMap.TryGetValue(symbolCodec.UnderlyingSymbol().Replace(".", "").ToUpper(), out double savedLastPrice))
                        {
                            if (!underlyingToLastPriceMap.TryGetValue(underlyingSymbol, out double lastPrice))
                            {
                                lastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingSymbol, SubscriptionFieldType.LastPrice);
                                underlyingToLastPriceMap[underlyingSymbol] = lastPrice;
                            }
                            changeInLastPrice = lastPrice - savedLastPrice;
                        }
                        else if (SavedLastPrice != 0)
                        {
                            if (!underlyingToLastPriceMap.TryGetValue(underlyingSymbol, out double lastPrice))
                            {
                                lastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingSymbol, SubscriptionFieldType.LastPrice);
                                underlyingToLastPriceMap[underlyingSymbol] = lastPrice;
                            }
                            changeInLastPrice = lastPrice - SavedLastPrice;
                        }

                        PutCall type = leg.callPut ? PutCall.Put : PutCall.Call;
                        options = options.Where(x => x.PutCall == type && x.Symbol != leg.symbol &&
                                               (x.Expiration - (leg.expiration + timeSpan)).TotalDays == 0).ToList();

                        if (MaxDte > 0)
                        {
                            options = options.Where(x => (x.Expiration.Date - DateTime.Today).TotalDays <= MaxDte).ToList();
                        }

                        Option swapLeg = options.MinBy(x => Math.Abs(x.Strike - (leg.strike + changeInLastPrice)));

                        if (swapLeg == null)
                        {
                            valid = false;
                            break;
                        }
                        Instrument newInstrument = new(swapLeg.Symbol)
                        {
                            buySell = leg.buySell,
                            ratio = leg.ratio
                        };
                        newSpread.AddLeg(newInstrument);
                    }
                    if (valid)
                    {
                        spreads.Add(newSpread);
                    }
                }
                catch (NullReferenceException) { }
                catch (ArgumentNullException) { }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(RollListAsyncCommand));
                }
            }
            return spreads;
        }

        private async Task<List<SymbolCodec>> RollSpreadsAndFillExpirations()
        {
            List<SymbolCodec> spreads = new();
            Dictionary<string, double> underlyingToLastPriceMap = new();
            TimeSpan timeSpan = TimeSpan.FromDays(DatesToRoll);
            for (int j = 0; j < RollRange; j++)
            {
                foreach (string spread in _spreads)
                {
                    try
                    {
                        SymbolCodec newSpread = new();
                        SymbolCodec symbolCodec = new(spread);
                        bool valid = true;
                        for (int legIndex = 0; legIndex < symbolCodec.LegCount; legIndex++)
                        {
                            Instrument leg = symbolCodec.GetLeg(legIndex);
                            string underlyingSymbol = await SwapUnderlyingAsync() ? Underlying : leg.underlyingSymbol;
                            List<Option> options = await OmsCore.QuoteClient.GetOptionsAsync(underlyingSymbol);

                            double changeInLastPrice = 0.0;
                            if (_underlyingToCreationLastPriceMap.TryGetValue(symbolCodec.UnderlyingSymbol().Replace(".", "").ToUpper(), out double savedLastPrice))
                            {
                                if (!underlyingToLastPriceMap.TryGetValue(underlyingSymbol, out double lastPrice))
                                {
                                    lastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingSymbol, SubscriptionFieldType.LastPrice);
                                    underlyingToLastPriceMap[underlyingSymbol] = lastPrice;
                                }
                                changeInLastPrice = lastPrice - savedLastPrice;
                            }
                            else if (SavedLastPrice != 0)
                            {
                                if (!underlyingToLastPriceMap.TryGetValue(underlyingSymbol, out double lastPrice))
                                {
                                    lastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingSymbol, SubscriptionFieldType.LastPrice);
                                    underlyingToLastPriceMap[underlyingSymbol] = lastPrice;
                                }
                                changeInLastPrice = lastPrice - SavedLastPrice;
                            }

                            PutCall type = leg.callPut ? PutCall.Put : PutCall.Call;
                            options = options.Where(x => x.PutCall == type && x.Expiration > leg.expiration)
                                             .GroupBy(option => (option.Expiration - (leg.expiration + timeSpan)).TotalDays)
                                             .OrderBy(x => x.Key)
                                             .ElementAt(j)
                                             .ToList();

                            if (MaxDte > 0)
                            {
                                options = options.Where(x => (x.Expiration.Date - DateTime.Today).TotalDays <= MaxDte).ToList();
                            }

                            Option swapLeg = options.MinBy(x => Math.Abs(x.Strike - (leg.strike + changeInLastPrice)));

                            if (swapLeg == null)
                            {
                                valid = false;
                                break;
                            }
                            Instrument newInstrument = new(swapLeg.Symbol)
                            {
                                buySell = leg.buySell,
                                ratio = leg.ratio
                            };
                            newSpread.AddLeg(newInstrument);
                        }
                        if (valid)
                        {
                            spreads.Add(newSpread);
                        }
                    }
                    catch (NullReferenceException) { }
                    catch (ArgumentNullException) { }
                    catch (ArgumentOutOfRangeException) { }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(RollListAsyncCommand));
                    }
                }
            }
            return spreads;
        }

        [Command]
        public void CancelCommand()
        {
            try
            {
                ShowProgressBar = false;
                ProgressStatus = "Operation Cancelled!";
                _cancellationTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelCommand));
            }
        }

        [Command]
        public async Task ExportAsyncCommand()
        {
            try
            {
                if (_latestSpreads != null && _latestSpreads.Count > 0)
                {
                    SaveFileDialogService.DefaultExt = "xlsx";
                    SaveFileDialogService.DefaultFileName = $"DOMINATOR SPREADS - {DateTime.Now:MM-dd-yyyy hh.mm} - {_latestSpreads.Count} spreads";
                    SaveFileDialogService.Filter = "Dominator List|*.XLSX";
                    bool save = SaveFileDialogService.ShowDialog();
                    if (save)
                    {
                        string filePath = SaveFileDialogService.GetFullFileName();

                        await Task.Run(() => ExportHelper.WriteSpreadsToFileUsingDominatorFormat(OmsCore.User.Username, filePath, _latestSpreads));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExportAsyncCommand));
            }
        }

        private DateTime ConvertToSpreadGeneratorResults(List<SymbolCodec> latestSpreads)
        {
            DateTime maxExp = DateTime.MinValue;
            Dictionary<Tuple<string, BaseStrategy>, SpreadGeneratorResults> output = new();
            foreach (SymbolCodec spread in latestSpreads)
            {
                try
                {
                    if (spread.LegCount == 0)
                    {
                        continue;
                    }

                    OptionsHelper.CorrectLegOrder(spread);
                    Spread spreadResult = new(spread.ToTOS());
                    if (spread.LegCount > 0)
                    {
                        Instrument leg = spread.GetLeg(0);
                        if (!leg.symbol.StartsWith('.'))
                        {
                            continue;
                        }
                        spreadResult.Legs.Add(new((OmsCore.SecurityBook.GetSecurity(leg.symbol) as Option), leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, leg.ratio));
                        if (leg.expiration > maxExp)
                        {
                            maxExp = leg.expiration;
                        }
                    }
                    if (spread.LegCount > 1)
                    {
                        Instrument leg = spread.GetLeg(1);
                        if (!leg.symbol.StartsWith('.'))
                        {
                            continue;
                        }
                        spreadResult.Legs.Add(new((OmsCore.SecurityBook.GetSecurity(leg.symbol) as Option), leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, leg.ratio));
                        if (leg.expiration > maxExp)
                        {
                            maxExp = leg.expiration;
                        }
                    }
                    if (spread.LegCount > 2)
                    {
                        Instrument leg = spread.GetLeg(2);
                        if (!leg.symbol.StartsWith('.'))
                        {
                            continue;
                        }
                        spreadResult.Legs.Add(new((OmsCore.SecurityBook.GetSecurity(leg.symbol) as Option), leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, leg.ratio));
                        if (leg.expiration > maxExp)
                        {
                            maxExp = leg.expiration;
                        }
                    }
                    if (spread.LegCount > 3)
                    {
                        Instrument leg = spread.GetLeg(3);
                        if (!leg.symbol.StartsWith('.'))
                        {
                            continue;
                        }
                        spreadResult.Legs.Add(new((OmsCore.SecurityBook.GetSecurity(leg.symbol) as Option), leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell, leg.ratio));
                        if (leg.expiration > maxExp)
                        {
                            maxExp = leg.expiration;
                        }
                    }

                    spreadResult.Symbol = spread.ToTOS();
                    if (OptionStrategy.TryIdentify(spreadResult.Symbol, out BaseStrategy baseStrategy, out _, out _))
                    {
                        string underlying = spread.UnderlyingSymbol();
                        Tuple<string, BaseStrategy> key = Tuple.Create(underlying, baseStrategy);
                        if (!output.TryGetValue(key, out SpreadGeneratorResults list))
                        {
                            list = new SpreadGeneratorResults(underlying, spreadResult.Legs[0]?.Option?.PutCall, baseStrategy.ToStrategy());
                            output[key] = list;
                        }
                        list.Spreads.Add(spreadResult);
                    }
                }
                catch (Exception) { }
            }

            LatestSpreadGeneratorResults = new ObservableCollection<SpreadGeneratorResults>();
            foreach (SpreadGeneratorResults result in output.Values)
            {
                result.UpdateExpirations();
                LatestSpreadGeneratorResults.Add(result);
            }
            return maxExp;
        }

        private async Task LoadSpreadsFromExcelDumpAsync(object[,] values)
        {
            try
            {
                await Task.Run(async () =>
                {
                    DateTime maxDate = DateTime.MinValue;
                    int start = values.GetLowerBound(0);
                    int count = values.GetUpperBound(0);
                    _spreads = new List<string>();
                    List<SymbolCodec> spreads = new();

                    for (int index = start; index <= count; index++)
                    {
                        if (_cancellationTokenSource.IsCancellationRequested)
                        {
                            break;
                        }
                        if (values[index, 7] is string spreadId)
                        {
                            _spreads.Add(spreadId);
                            SymbolCodec spread = new(spreadId);
                            spreads.Add(spread);
                        }
                    }

                    ParseCreationDate(values[0, 21]);
                    await ParseLastPriceAsync(values[0, 20], values[1, 22]);

                    DateTime maxExp = ConvertToSpreadGeneratorResults(spreads);
                    if (CreationDate.HasValue && CreationDate.Value.Date < maxExp.Date)
                    {
                        MaxDte = (maxExp.Date - CreationDate.Value.Date).TotalDays;
                    }
                });
            }
            finally
            {
                ShowProgressBar = false;
                ProgressStatus = "List Loaded!";
            }
        }

        private void ParseCreationDate(object dateCellValue)
        {
            try
            {
                if (dateCellValue is string creationDateString &&
                       DateTime.TryParseExact(creationDateString, "M.d.yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
                {
                    CreationDate = dateTime;
                }
                else
                {
                    CreationDate = File.GetCreationTime(InputListPath);
                }

                double totalDays = (DateTime.Today - CreationDate.Value).TotalDays;
                if (totalDays > 0)
                {
                    DatesToRoll = totalDays;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ParseCreationDate));
            }
        }

        private async Task ParseLastPriceAsync(object underlyingValue, object lastMapValue)
        {
            try
            {
                _underlyingToCreationLastPriceMap = new Dictionary<string, double>();
                if (lastMapValue is string lastPriceString && !string.IsNullOrWhiteSpace(lastPriceString) && underlyingValue is string underlying && !string.IsNullOrWhiteSpace(underlying))
                {
                    string[] symbolLastPricePairs = lastPriceString.Split(';');
                    foreach (string symbolLastPrice in symbolLastPricePairs)
                    {
                        string[] mapping = symbolLastPrice.Split(',');
                        if (mapping.Length > 1)
                        {
                            string symbol = mapping[0];
                            string lastPrice = mapping[1];
                            if (!string.IsNullOrWhiteSpace(symbol) && double.TryParse(lastPrice, out double price))
                            {
                                _underlyingToCreationLastPriceMap[symbol.ToUpper()] = price;
                            }
                        }
                        else if (mapping.Length == 1)
                        {
                            string lastPrice = mapping[0];
                            if (double.TryParse(lastPrice, out double price))
                            {
                                SavedLastPrice = price;
                                double newLastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlying, SubscriptionFieldType.LastPrice);
                                ChangeInLastPrice = newLastPrice - SavedLastPrice;
                            }
                        }
                    }
                }
                else if (lastMapValue is double price && underlyingValue is string underlyingString && !string.IsNullOrWhiteSpace(underlyingString))
                {
                    double newLastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlyingString, SubscriptionFieldType.LastPrice);
                    SavedLastPrice = price;
                    ChangeInLastPrice = newLastPrice - price;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ParseLastPriceAsync));
            }
        }

        private void ProgressUpdate(double progressValue)
        {
            if (ProgressValue != progressValue)
            {
                ProgressValue = progressValue;
            }
        }

        private async Task<bool> SwapUnderlyingAsync()
        {
            if (!string.IsNullOrEmpty(Underlying))
            {
                List<Option> options = await OmsCore.QuoteClient.GetOptionsAsync(Underlying);
                return options.Count > 0;
            }
            return false;
        }
    }
}
