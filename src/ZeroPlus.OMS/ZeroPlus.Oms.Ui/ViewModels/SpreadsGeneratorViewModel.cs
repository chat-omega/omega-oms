using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Spreadsheet;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Extensions;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using Strategy = ZeroPlus.Models.Generators.SpreadGenerators.Strategy;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class SpreadsGeneratorViewModel : ModuleViewModelBase, ISpreadsGenerator, IOrderArchiveReceiver
    {
        private static readonly string MODULE_TITLE = "Spreads Generator";

        internal static readonly JsonSerializerSettings SpreadGeneratorConfigSerializationSettings = new()
        {
            Converters =
            {
                new InterfaceToConcreteConverter<ISpreadGeneratorIntFilter, SpreadGeneratorIntFilterModel>(),
                new InterfaceToConcreteConverter<ISingleLegSpreadsGeneratorSettings, SingleLegSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IVerticalSpreadsGeneratorSettings, VerticalSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IRatioSpreadsGeneratorSettings, RatioSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<ICalendarSpreadsGeneratorSettings, CalendarSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IDiagonalSpreadsGeneratorSettings, DiagonalSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IButterflySpreadsGeneratorSettings, ButterflySpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<ISkewedButterflySpreadsGeneratorSettings, SkewedButterflySpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<ITreeSpreadsGeneratorSettings, TreeSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<ICalendarButterflySpreadsGeneratorSettings, CalendarButterflySpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IIronButterflySpreadsGeneratorSettings, IronFlySpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IIronGutFlySpreadsGeneratorSettings, IronFlySpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<ICondorSpreadsGeneratorSettings, CondorSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IOneThreeThreeOneSpreadsGeneratorSettings, OneThreeThreeOneSpreadsSettingsModel>(),
                new InterfaceToConcreteConverter<IIronCondorSpreadsGeneratorSettings, IronCondorSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IOneThreeTwoSpreadsGeneratorSettings, OneThreeTwoSpreadsGeneratorSettingsModel>(),
                new InterfaceToConcreteConverter<IBoxSpreadsGeneratorSettings, BoxSpreadsGeneratorSettingsModel>(),
            }
        };

        private readonly DataStore _deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _theoStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _vegaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _bidStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _askStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _lastStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _emaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
        private readonly DataStore _volaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);

        private readonly TransactionConsumerModel _transactionConsumerModel;

        private CancellationTokenSource _cancellationTokenSource = new();
        private DelegateCommand<ConfigSave> _openInBasketTraderCommand;
        private DelegateCommand _openInDominatorCommand;
        private DelegateCommand _openInLockTraderCommand;
        private readonly ConcurrentDictionary<string, (HashSet<string> Puts, HashSet<string> Calls)> _underlyingToFishedExpirationsMap = new();
        private readonly ManualResetEventSlim _waitForOrderLoadEvent = new(false);

        private readonly object _bufferLock = new();
        private readonly Queue<IOrder> _buffer = new();
        private readonly PortfolioManagerModel _portfolioManager;
        private readonly IModuleFactory _moduleFactory;

        private DateTime _MinExp;
        private DateTime _MaxExp;
        private ObservableCollection<ConfigChainModel> _ConfigChains;

        public SingleLegSpreadsGenerator SingleLegSpreadsGenerator { get; set; }
        public VerticalSpreadsGenerator VerticalSpreadsGenerator { get; set; }
        public RatioSpreadsGenerator OneByTwoRatioSpreadsGenerator { get; set; }
        public RatioSpreadsGenerator OneByThreeRatioSpreadsGenerator { get; set; }
        public RatioSpreadsGenerator RatioSpreadsGenerator { get; set; }
        public CalendarSpreadsGenerator CalendarSpreadsGenerator { get; set; }
        public DiagonalSpreadsGenerator DiagonalSpreadsGenerator { get; set; }
        public ButterflySpreadsGenerator ButterflySpreadsGenerator { get; set; }
        public SkewedButterflySpreadsGenerator SkewedButterflySpreadsGenerator { get; set; }
        public TreeSpreadsGenerator TreeSpreadsGenerator { get; set; }
        public CalendarButterflySpreadsGenerator CalendarButterflySpreadsGenerator { get; set; }
        public IronButterflySpreadsGenerator IronButterflySpreadsGenerator { get; set; }
        public IronGutFlyGenerator IronGutFlyGenerator { get; set; }
        public CondorSpreadsGenerator CondorSpreadsGenerator { get; set; }
        public IronCondorSpreadsGenerator IronCondorSpreadsGenerator { get; set; }
        public OneThreeThreeOneSpreadsGenerator OneThreeThreeOneSpreadsGenerator { get; set; }
        public OneThreeTwoSpreadsGenerator OneThreeTwoSpreadsGenerator { get; set; }
        public OneThreeTwoSpreadsGenerator TwoThreeOneSpreadsGenerator { get; set; }
        public BoxSpreadsGenerator BoxSpreadsGenerator { get; set; }

        public ICommand OpenInBasketTraderWithConfigCommand => _openInBasketTraderCommand ??= new DelegateCommand<ConfigSave>(OpenInBasketTraderWithConfig);
        public ICommand OpenInDominatorCommand => _openInDominatorCommand ??= new DelegateCommand(OpenInDominator);
        public ICommand OpenInLockTraderCommand => _openInLockTraderCommand ??= new DelegateCommand(OpenInLockTrader);

        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();
        protected ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public IEnumerable<RunnerOption> RunnerOptions { get; } = Enum.GetValues<RunnerOption>();
        public IEnumerable<OutputSortingMode> TopPercentageSelectionSortingModes { get; } = Enum.GetValues<OutputSortingMode>();

        public override Module Module { get; protected set; } = Module.SpreadsGenerator;
        public TheoModel[] TheoModels { get; } = Enum.GetValues<TheoModel>();
        public List<string> SupportedFormats { get; set; } = ["Dominator List", "CSV"];

        [Bindable]
        public partial ObservableCollection<SpreadGeneratorResults> LatestSpreadGeneratorResults { get; set; }
        [Bindable(Default = RunnerOption.Server)]
        public partial RunnerOption RunnerOption { get; set; }
        [Bindable]
        public partial double ParserFrontMonthPercent { get; set; }
        [Bindable]
        public partial double ParserBackMonthPercent { get; set; }
        [Bindable]
        public partial int ParserMinCount { get; set; }
        [Bindable]
        public partial int ParserGroupTotalCount { get; set; }
        [Bindable]
        public partial string ExportFormat { get; set; }
        [Bindable]
        public partial bool RandomizeExport { get; set; }
        [Bindable]
        public partial bool CrateSeparateExportForUnderlyings { get; set; }
        [Bindable]
        public partial bool CanExport { get; set; }
        [Bindable]
        public partial string FilePath { get; set; }
        [Bindable]
        public partial bool SaveWhenDone { get; set; }
        [Bindable]
        public partial bool ShowProgressBar { get; set; }
        [Bindable]
        public partial string ProgressStatus { get; set; }
        [Bindable]
        public partial bool CallsEnabled { get; set; }
        [Bindable]
        public partial bool PutsEnabled { get; set; }
        [Bindable]
        public partial bool WholeStrikes { get; set; }
        [Bindable]
        public partial bool DecimalStrikes { get; set; }
        [Bindable]
        public partial bool MinStrikeEnabled { get; set; }
        [Bindable]
        public partial bool MaxStrikeEnabled { get; set; }
        [Bindable]
        public partial double MinStrike { get; set; }
        [Bindable]
        public partial double MaxStrike { get; set; }
        [Bindable]
        public partial bool MinStrikeOccurrenceEnabled { get; set; }
        [Bindable]
        public partial bool MaxStrikeOccurrenceEnabled { get; set; }
        [Bindable]
        public partial int MinStrikeOccurrence { get; set; }
        [Bindable]
        public partial int MaxStrikeOccurrence { get; set; }
        [Bindable]
        public partial bool StrikeDistanceFromLastPercentEnabled { get; set; }
        [Bindable]
        public partial double StrikeDistanceFromLastPercent { get; set; }
        [Bindable]
        public partial bool StrikeIncludeFromTopAndBottomEnabled { get; set; }
        [Bindable]
        public partial bool StrikeIncludeAsAdditionFromTopAndBottom { get; set; }
        [Bindable]
        public partial int StrikeIncludeFromTopAndBottomCount { get; set; }
        [Bindable]
        public partial bool MinExpEnabled { get; set; }
        [Bindable]
        public partial bool MaxExpEnabled { get; set; }
        [Bindable]
        public partial bool Regulars { get; set; }
        [Bindable]
        public partial bool Quarterlies { get; set; }
        partial void OnFreshSpreadsChanged(bool value)
        {
            if (value)
                AttemptedSpreads = false;
        }
        [Bindable]
        public partial bool FreshSpreads { get; set; }
        partial void OnAttemptedSpreadsChanged(bool value)
        {
            if (value)
                FreshSpreads = false;
        }
        [Bindable]
        public partial bool AttemptedSpreads { get; set; }
        [Bindable]
        public partial bool DteRangeEnabled { get; set; }
        [Bindable]
        public partial bool ApplyDteToExpSpacingMap { get; set; }
        [Bindable]
        public partial int MinDteRange { get; set; }
        [Bindable]
        public partial int MaxDteRange { get; set; }
        [Bindable]
        public partial bool NonRegulars { get; set; }
        [Bindable]
        public partial bool Leg1LockEnabled { get; set; }
        [Bindable]
        public partial bool Leg2LockEnabled { get; set; }
        [Bindable]
        public partial bool Leg3LockEnabled { get; set; }
        [Bindable]
        public partial bool Leg4LockEnabled { get; set; }
        public DateTime MinExp
        {
            get => _MinExp;
            set => SetValue(ref _MinExp, value.Date);
        }
        public DateTime MaxExp
        {
            get => _MaxExp;
            set => SetValue(ref _MaxExp, value.Date + TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(59));
        }
        [Bindable]
        public partial double ExpirationSpacingCurveNear { get; set; }
        [Bindable]
        public partial double ExpirationSpacingCurveMid { get; set; }
        [Bindable]
        public partial double ExpirationSpacingCurveFar { get; set; }
        [Bindable]
        public partial ObservableCollection<ExpirationModel> ExpirationsList { get; set; }
        [Bindable]
        public partial ObservableCollection<ExpirationModel> CalendarTargetExpirationsList { get; set; }
        [Bindable]
        public partial ObservableCollection<ExpirationModel> DiagonalTargetExpirationsList { get; set; }
        [Bindable]
        public partial string Leg1LockOptions { get; set; }
        [Bindable]
        public partial string Leg2LockOptions { get; set; }
        [Bindable]
        public partial string Leg3LockOptions { get; set; }
        [Bindable]
        public partial string Leg4LockOptions { get; set; }
        [Bindable]
        public partial bool CalendarTargetExpirationListEnabled { get; set; }
        [Bindable]
        public partial bool DiagonalTargetExpirationListEnabled { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg1OpenInterestFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg2OpenInterestFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg3OpenInterestFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg4OpenInterestFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg1VolumeFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg2VolumeFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg3VolumeFilter { get; set; }
        [Bindable]
        public partial ISpreadGeneratorIntFilter Leg4VolumeFilter { get; set; }
        [Bindable]
        public partial bool TopPercentageSelectionEnabled { get; set; }
        [Bindable]
        public partial double TopPercentageSelectionCount { get; set; }
        [Bindable]
        public partial OutputSortingMode TopPercentageSelectionSortingMode { get; set; }
        [Bindable]
        public partial bool MaxCountEnabled { get; set; }
        [Bindable]
        public partial bool ParsedOutputEnabled { get; set; }
        [Bindable]
        public partial int ParsedBuildCount { get; set; }
        [Bindable]
        public partial int ParsedOutputCount { get; set; }
        [Bindable]
        public partial bool ApproximateMissingQuotes { get; set; }
        [Bindable]
        public partial bool ApproximateMissingGreeks { get; set; }
        [Bindable]
        public partial bool ApproximateMissingHanweck { get; set; }
        [Bindable]
        public partial int MaxCount { get; set; }
        [Bindable]
        public partial bool SelectorEnabled { get; set; }
        [Bindable]
        public partial int SelectorCount { get; set; }
        [Bindable]
        public partial bool RandomSelector { get; set; }
        [Bindable]
        public partial bool SingleLegEnabled { get; set; }
        [Bindable]
        public partial bool VerticalEnabled { get; set; }
        [Bindable]
        public partial bool OneByTwoRatioEnabled { get; set; }
        [Bindable]
        public partial bool OneByThreeRatioEnabled { get; set; }
        [Bindable]
        public partial bool RatioEnabled { get; set; }
        [Bindable]
        public partial bool ButterflyEnabled { get; set; }
        [Bindable]
        public partial bool SkewedButterflyEnabled { get; set; }
        [Bindable]
        public partial bool TreeEnabled { get; set; }
        [Bindable]
        public partial bool CalendarButterflyEnabled { get; set; }
        [Bindable]
        public partial bool IronButterflyEnabled { get; set; }
        [Bindable]
        public partial bool IronGutFlyEnabled { get; set; }
        [Bindable]
        public partial bool CalendarEnabled { get; set; }
        [Bindable]
        public partial bool DiagonalEnabled { get; set; }
        [Bindable]
        public partial bool CondorEnabled { get; set; }
        [Bindable]
        public partial bool IronCondorEnabled { get; set; }
        [Bindable]
        public partial bool OneThreeThreeOneEnabled { get; set; }
        [Bindable]
        public partial bool OneThreeTwoEnabled { get; set; }
        [Bindable]
        public partial bool TwoThreeOneEnabled { get; set; }
        [Bindable]
        public partial bool BoxEnabled { get; set; }
        [Bindable]
        public partial bool ConfigChainEnabled { get; set; }
        [Bindable]
        public partial bool ExportToFile { get; set; }
        [Bindable]
        public partial bool OpenInBasket { get; set; }
        [Bindable]
        public partial bool AllowGenerating { get; set; }
        [Bindable]
        public partial bool Generating { get; set; }
        [Bindable]
        public partial string UnderlyingQuery { get; set; }
        [Bindable]
        public partial string SingleLegCallSpreadSample { get; set; }
        [Bindable]
        public partial string VerticalCallSpreadSample { get; set; }
        [Bindable]
        public partial string OneByTwoRatioCallSpreadSample { get; set; }
        [Bindable]
        public partial string OneByThreeRatioCallSpreadSample { get; set; }
        [Bindable]
        public partial string RatioCallSpreadSample { get; set; }
        [Bindable]
        public partial string CalendarCallSpreadSample { get; set; }
        [Bindable]
        public partial string DiagonalCallSpreadSample { get; set; }
        [Bindable]
        public partial string ButterflyCallSpreadSample { get; set; }
        [Bindable]
        public partial string OneThreeTwoCallSpreadSample { get; set; }
        [Bindable]
        public partial string TwoThreeOneCallSpreadSample { get; set; }
        [Bindable]
        public partial string SkewedButterflyCallSpreadSample { get; set; }
        [Bindable]
        public partial string TreeCallSpreadSample { get; set; }
        [Bindable]
        public partial string CalendarButterflyCallSpreadSample { get; set; }
        [Bindable]
        public partial string IronButterflySpreadSample { get; set; }
        [Bindable]
        public partial string IronGutFlySpreadSample { get; set; }
        [Bindable]
        public partial string CondorCallSpreadSample { get; set; }
        [Bindable]
        public partial string OneThreeThreeOneCallSpreadSample { get; set; }
        [Bindable]
        public partial string IronCondorSpreadSample { get; set; }
        [Bindable]
        public partial string SingleLegPutSpreadSample { get; set; }
        [Bindable]
        public partial string VerticalPutSpreadSample { get; set; }
        [Bindable]
        public partial string OneByTwoRatioPutSpreadSample { get; set; }
        [Bindable]
        public partial string OneByThreeRatioPutSpreadSample { get; set; }
        [Bindable]
        public partial string RatioPutSpreadSample { get; set; }
        [Bindable]
        public partial string CalendarPutSpreadSample { get; set; }
        [Bindable]
        public partial string DiagonalPutSpreadSample { get; set; }
        [Bindable]
        public partial string ButterflyPutSpreadSample { get; set; }
        [Bindable]
        public partial string SkewedButterflyPutSpreadSample { get; set; }
        [Bindable]
        public partial string TreePutSpreadSample { get; set; }
        [Bindable]
        public partial string CalendarButterflyPutSpreadSample { get; set; }
        [Bindable]
        public partial string CondorPutSpreadSample { get; set; }
        [Bindable]
        public partial string OneThreeThreeOnePutSpreadSample { get; set; }
        [Bindable]
        public partial string OneThreeTwoPutSpreadSample { get; set; }
        [Bindable]
        public partial string TwoThreeOnePutSpreadSample { get; set; }
        [Bindable]
        public partial string BoxSpreadSample { get; set; }
        [Bindable]
        public partial ObservableCollection<OptionChainModel> OptionChains { get; set; }
        [Bindable]
        public partial ObservableCollection<SpreadGeneratorStat> VerticalStats { get; set; }
        [Bindable]
        public partial SingleLegSpreadsGeneratorSettingsModel SingleLegSpreadsSettings { get; set; }
        [Bindable]
        public partial VerticalSpreadsGeneratorSettingsModel VerticalSpreadsSettings { get; set; }
        [Bindable]
        public partial RatioSpreadsGeneratorSettingsModel OneByTwoRatioSpreadsSettings { get; set; }
        [Bindable]
        public partial RatioSpreadsGeneratorSettingsModel OneByThreeRatioSpreadsSettings { get; set; }
        [Bindable]
        public partial RatioSpreadsGeneratorSettingsModel RatioSpreadsSettings { get; set; }
        [Bindable]
        public partial CalendarSpreadsGeneratorSettingsModel CalendarSpreadsSettings { get; set; }
        [Bindable]
        public partial DiagonalSpreadsGeneratorSettingsModel DiagonalSpreadsSettings { get; set; }
        [Bindable]
        public partial ButterflySpreadsGeneratorSettingsModel ButterflySpreadsSettings { get; set; }
        [Bindable]
        public partial SkewedButterflySpreadsGeneratorSettingsModel SkewedButterflySpreadsSettings { get; set; }
        [Bindable]
        public partial TreeSpreadsGeneratorSettingsModel TreeSpreadsSettings { get; set; }
        [Bindable]
        public partial CalendarButterflySpreadsGeneratorSettingsModel CalendarButterflySpreadsSettings { get; set; }
        [Bindable]
        public partial IronFlySpreadsGeneratorSettingsModel IronButterflySpreadsSettings { get; set; }
        [Bindable]
        public partial IronFlySpreadsGeneratorSettingsModel IronGutFlySpreadsSettings { get; set; }
        [Bindable]
        public partial CondorSpreadsGeneratorSettingsModel CondorSpreadsSettings { get; set; }
        [Bindable]
        public partial IronCondorSpreadsGeneratorSettingsModel IronCondorSpreadsSettings { get; set; }
        [Bindable]
        public partial OneThreeThreeOneSpreadsSettingsModel OneThreeThreeOneSpreadsSettings { get; set; }
        [Bindable]
        public partial OneThreeTwoSpreadsGeneratorSettingsModel OneThreeTwoSpreadsSettings { get; set; }
        [Bindable]
        public partial OneThreeTwoSpreadsGeneratorSettingsModel TwoThreeOneSpreadsSettings { get; set; }
        [Bindable]
        public partial BoxSpreadsGeneratorSettingsModel BoxSpreadsGeneratorSettings { get; set; }
        public ObservableCollection<ConfigChainModel> ConfigChains
        {

            get => _ConfigChains;
            set => SetValue(ref _ConfigChains, value);
        }
        public IEnumerable<ISpreadsGeneratorSettings> SpreadGeneratorSettings { get; }
        public HashSet<string> Options { get; private set; }


        public SpreadsGeneratorViewModel(Microsoft.Extensions.Logging.ILogger<SpreadsGeneratorViewModel> logger,
                                         OmsCore omsCore,
                                         TransactionConsumerModel transactionConsumerModel,
                                         PortfolioManagerModel portfolioManagerModel,
                                         ConfigBrowserViewModel configBrowserViewModel,
                                         IModuleFactory moduleFactory) : base(configBrowserViewModel, omsCore)
        {
            _portfolioManager = portfolioManagerModel;
            _moduleFactory = moduleFactory;
            _transactionConsumerModel = transactionConsumerModel;

            ModuleTitle = MODULE_TITLE;
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;
            ConfigBrowserViewModel = configBrowserViewModel;
            ConfigBrowserViewModel.LoadConfig = LoadSavedConfig;
            ConfigBrowserViewModel.Module = Module.SpreadsGenerator.ToString();

            LatestSpreadGeneratorResults = new();

            ExportFormat = SupportedFormats.FirstOrDefault();
            RandomizeExport = true;
            OptionChains = new ObservableCollection<OptionChainModel>();
            ConfigChains = new ObservableCollection<ConfigChainModel>();
            ExpirationsList = new ObservableCollection<ExpirationModel>();
            CalendarTargetExpirationsList = new ObservableCollection<ExpirationModel>();
            DiagonalTargetExpirationsList = new ObservableCollection<ExpirationModel>();
            MinExp = DateTime.Today;
            MaxExp = DateTime.Today;

            CallsEnabled = true;
            PutsEnabled = true;

            VerticalStats = new ObservableCollection<SpreadGeneratorStat>();

            SingleLegSpreadsSettings = new SingleLegSpreadsGeneratorSettingsModel();
            VerticalSpreadsSettings = new VerticalSpreadsGeneratorSettingsModel();
            OneByTwoRatioSpreadsSettings = new RatioSpreadsGeneratorSettingsModel()
            {
                Leg1Ratio = 1,
                Leg2Ratio = 2,
            };
            OneByThreeRatioSpreadsSettings = new RatioSpreadsGeneratorSettingsModel()
            {
                Leg1Ratio = 1,
                Leg2Ratio = 3,
            };

            Regulars = true;
            NonRegulars = true;
            Quarterlies = true;

            WholeStrikes = true;
            DecimalStrikes = true;

            RatioSpreadsSettings = new RatioSpreadsGeneratorSettingsModel();
            CalendarSpreadsSettings = new CalendarSpreadsGeneratorSettingsModel();
            DiagonalSpreadsSettings = new DiagonalSpreadsGeneratorSettingsModel();
            ButterflySpreadsSettings = new ButterflySpreadsGeneratorSettingsModel();
            SkewedButterflySpreadsSettings = new SkewedButterflySpreadsGeneratorSettingsModel();
            TreeSpreadsSettings = new TreeSpreadsGeneratorSettingsModel();
            CalendarButterflySpreadsSettings = new CalendarButterflySpreadsGeneratorSettingsModel();
            IronButterflySpreadsSettings = new IronFlySpreadsGeneratorSettingsModel();
            IronGutFlySpreadsSettings = new IronFlySpreadsGeneratorSettingsModel();
            CondorSpreadsSettings = new CondorSpreadsGeneratorSettingsModel();
            IronCondorSpreadsSettings = new IronCondorSpreadsGeneratorSettingsModel();
            OneThreeThreeOneSpreadsSettings = new OneThreeThreeOneSpreadsSettingsModel();
            OneThreeTwoSpreadsSettings = new OneThreeTwoSpreadsGeneratorSettingsModel();
            TwoThreeOneSpreadsSettings = new OneThreeTwoSpreadsGeneratorSettingsModel()
            {
                Reversed = true
            };
            BoxSpreadsGeneratorSettings = new BoxSpreadsGeneratorSettingsModel();

            SpreadGeneratorSettings =
            [
                SingleLegSpreadsSettings,
                VerticalSpreadsSettings,
                OneByTwoRatioSpreadsSettings,
                OneByThreeRatioSpreadsSettings,
                RatioSpreadsSettings,
                CalendarSpreadsSettings,
                DiagonalSpreadsSettings,
                ButterflySpreadsSettings,
                SkewedButterflySpreadsSettings,
                TreeSpreadsSettings,
                CalendarButterflySpreadsSettings,
                IronButterflySpreadsSettings,
                IronGutFlySpreadsSettings,
                CondorSpreadsSettings,
                IronCondorSpreadsSettings,
                OneThreeThreeOneSpreadsSettings,
                OneThreeTwoSpreadsSettings,
                TwoThreeOneSpreadsSettings,
                BoxSpreadsGeneratorSettings,
            ];

            SingleLegSpreadsGenerator = new SingleLegSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            VerticalSpreadsGenerator = new VerticalSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            OneByTwoRatioSpreadsGenerator = new RatioSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            OneByThreeRatioSpreadsGenerator = new RatioSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            RatioSpreadsGenerator = new RatioSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            CalendarSpreadsGenerator = new CalendarSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            DiagonalSpreadsGenerator = new DiagonalSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            ButterflySpreadsGenerator = new ButterflySpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            SkewedButterflySpreadsGenerator = new SkewedButterflySpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            TreeSpreadsGenerator = new TreeSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            CalendarButterflySpreadsGenerator = new CalendarButterflySpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            IronButterflySpreadsGenerator = new IronButterflySpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            IronGutFlyGenerator = new IronGutFlyGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            CondorSpreadsGenerator = new CondorSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            IronCondorSpreadsGenerator = new IronCondorSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            OneThreeThreeOneSpreadsGenerator = new OneThreeThreeOneSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            OneThreeTwoSpreadsGenerator = new OneThreeTwoSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            TwoThreeOneSpreadsGenerator = new OneThreeTwoSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);
            BoxSpreadsGenerator = new BoxSpreadsGenerator(logger, _deltaStore, _theoStore, _vegaStore, _bidStore, _askStore, _lastStore, _emaStore, _vegaStore, _volaStore);

            Leg1OpenInterestFilter = new SpreadGeneratorIntFilterModel();
            Leg2OpenInterestFilter = new SpreadGeneratorIntFilterModel();
            Leg3OpenInterestFilter = new SpreadGeneratorIntFilterModel();
            Leg4OpenInterestFilter = new SpreadGeneratorIntFilterModel();
            Leg1VolumeFilter = new SpreadGeneratorIntFilterModel();
            Leg2VolumeFilter = new SpreadGeneratorIntFilterModel();
            Leg3VolumeFilter = new SpreadGeneratorIntFilterModel();
            Leg4VolumeFilter = new SpreadGeneratorIntFilterModel();
        }

        public override void OnDispose()
        {
            try
            {
                base.OnDispose();
                OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;

                if (Generating)
                {
                    Cancel();
                }

                _deltaStore.Dispose();
                _theoStore.Dispose();
                _bidStore.Dispose();
                _emaStore.Dispose();
                _lastStore.Dispose();
                _vegaStore.Dispose();
                _askStore.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnDispose));
            }
        }

        [Command]
        public void AddConfigToChainCommand()
        {
            ConfigChainModel model = new(this, ConfigBrowserViewModel.Configs.SelectMany(x => x.Configs).ToList());
            ConfigChains.Add(model);
        }

        [Command]
        public void RemoveConfigFromChainCommand(ConfigChainModel model)
        {
            ConfigChains.Remove(model);
        }

        [Command]
        public void ClearSymbols()
        {
            try
            {
                OptionChains.Clear();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearSymbols));
            }
        }

        [Command]
        public void LoadSavedConfig(ConfigSave configSave)
        {
            try
            {
                if (configSave == null)
                {
                    return;
                }
                OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id)
                    .ContinueWith(t => Dispatcher?.BeginInvoke(() => LoadConfigFromJsonAsync(t.Result.ConfigJson)));
                ModuleTitle = configSave.Title + " - " + MODULE_TITLE;
            }
            catch (AggregateException ae)
            {
                foreach (Exception ex in ae.InnerExceptions)
                {
                    _log.Error(ex, nameof(LoadSavedConfig));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadSavedConfig));
            }
        }

        [Command]
        public void ShowOptionSelectorCommand(string which)
        {
            OptionSelectorView view = new OptionSelectorView();
            if (view.ViewModel == null)
            {
                return;
            }

            view.ViewModel.WhichLeg = which;
            view.ViewModel.Options = Options;
            view.ViewModel.SelectionChanged += LegLockSelectionChanged;
            view.ShowDialog();
            view.ViewModel.SelectionChanged -= LegLockSelectionChanged;
        }

        private void LegLockSelectionChanged(string leg, string selection)
        {
            switch (leg)
            {
                case "Leg1":
                    Leg1LockOptions = selection;
                    break;
                case "Leg2":
                    Leg2LockOptions = selection;
                    break;
                case "Leg3":
                    Leg3LockOptions = selection;
                    break;
                case "Leg4":
                    Leg4LockOptions = selection;
                    break;
            }
        }

        [Command]
        public void ShareConfig()
        {
            try
            {
                ShareWithView view = new();

                if (view.DataContext is ShareWithViewModel viewModel)
                {
                    viewModel.Module = Module.SpreadsGenerator;
                    SpreadsGeneratorConfig config = GetConfig();
                    viewModel.Config = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented,
                        SpreadGeneratorConfigSerializationSettings);
                }

                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareConfig));
            }
        }

        [Command]
        public void SaveConfig()
        {
            try
            {
                SaveView view = new();

                if (view.DataContext is SaveViewModel viewModel)
                {
                    viewModel.LoadGroups(Module.SpreadsGenerator);
                    viewModel.ShowDefault = false;
                    SpreadsGeneratorConfig config = GetConfig();
                    viewModel.Config = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented,
                        SpreadGeneratorConfigSerializationSettings);
                    if (ConfigSave != null)
                    {
                        viewModel.Id = ConfigSave.Id;
                        viewModel.Title = ConfigSave.Title;
                        viewModel.SelectedGroup = ConfigSave.Group;
                    }

                    view.ShowDialog();

                    if (!string.IsNullOrWhiteSpace(viewModel.Title) && viewModel.Success)
                    {
                        ModuleTitle = viewModel.Title + " - " + MODULE_TITLE;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveConfig));
            }
        }

        [Command]
        public void PasteFromClipboard()
        {
            UnderlyingQuery = Clipboard.GetText().Trim().Replace(Environment.NewLine, ",");
        }

        [Command]
        public async Task SearchUnderlying()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(UnderlyingQuery))
                {
                    AllowGenerating = false;
                    IEnumerable<string> symbols = UnderlyingQuery.Replace(",", ";")
                                                 .Split(';')
                                                 .Where(x => !string.IsNullOrWhiteSpace(x))
                                                 .Select(x => x.Trim().ToUpper())
                                                 .Select(x => OptionsHelper.IsIndex(x) ? "$" + x : x)
                                                 .Distinct();

                    List<Task> getOptionsTasks = new();
                    ConcurrentBag<OptionChainModel> results = new();
                    foreach (string symbol in symbols)
                    {
                        Task task = OmsCore.QuoteClient.GetSymbolsAsync(symbol)
                                                      .ContinueWith(t => results.Add(new OptionChainModel(OmsCore.SecurityBook, t.Result)));
                        getOptionsTasks.Add(task);
                    }

                    await Task.WhenAll(getOptionsTasks);
                    await AddMultipleOptionChainsAsync(results.ToList());
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchUnderlying));
            }
        }

        [Command]
        public void RemoveOptionChain(OptionChainModel optionChain)
        {
            if (OptionChains.Any(x => x.Symbol == optionChain.Symbol))
            {
                DispatcherService?.BeginInvoke(() =>
                {
                    OptionChains.Remove(optionChain);
                }).ContinueWith(_ => CheckIfGeneratingIsAllowed())
                  .ContinueWith(_ => CalculateStatsAndSamples());
            }
        }

        [Command]
        public void CheckAllExpirationCommand()
        {
            foreach (ExpirationModel expiration in ExpirationsList)
            {
                expiration.IsChecked = true;
            }
        }

        [Command]
        public void UncheckAllExpirationCommand()
        {
            foreach (ExpirationModel expiration in ExpirationsList)
            {
                expiration.IsChecked = false;
            }
        }

        [Command]
        public void CheckAllCalendarTargetExpirationCommand()
        {
            foreach (ExpirationModel expiration in CalendarTargetExpirationsList)
            {
                expiration.IsChecked = true;
            }
        }

        [Command]
        public void UncheckAllCalendarTargetExpirationCommand()
        {
            foreach (ExpirationModel expiration in CalendarTargetExpirationsList)
            {
                expiration.IsChecked = false;
            }
        }

        [Command]
        public void CheckAllDiagonalTargetExpirationCommand()
        {
            foreach (ExpirationModel expiration in DiagonalTargetExpirationsList)
            {
                expiration.IsChecked = true;
            }
        }

        [Command]
        public void UncheckAllDiagonalTargetExpirationCommand()
        {
            foreach (ExpirationModel expiration in DiagonalTargetExpirationsList)
            {
                expiration.IsChecked = false;
            }
        }

        [Command]
        public void MinExpirationChangedCommand(DateTime minExp)
        {
            MinExp = minExp;
        }

        [Command]
        public void MaxExpirationChangedCommand(DateTime maxExp)
        {
            MaxExp = maxExp;
        }

        [Command]
        public void CalculateStatsAndSamples()
        {
            Task.Run(GenerateSampleSpreads);
        }

        [Command]
        public void OpenInQuotesAndGreeksBoardCommand()
        {
            if (_moduleFactory.CreateModule(Module.QuotesAndGreeksBoard) is QuotesAndGreeksBoardView { ViewModel: QuotesAndGreeksBoardViewModel viewModel })
            {
                if (viewModel.IsReady)
                {
                    Task.Run(() => OnReady(viewModel));
                }
                else
                {
                    viewModel.Ready += OnReady;
                }

                void OnReady(IModuleViewModel _)
                {
                    viewModel.Ready -= OnReady;
                    viewModel.LoadSymbols(LatestSpreadGeneratorResults.SelectMany(x => x.Spreads).Select(x => x.Symbol).ToList());
                }
            }
        }

        private void OpenInDominator()
        {
            var factory = App.AppHost.Services.GetService(typeof(IModuleFactory)) as IModuleFactory;
            if (factory?.CreateModule(Module.NewDominatorManager) is NewDominatorManager window)
            {
                NewDominatorManagerViewModel viewModel = (NewDominatorManagerViewModel)window.ViewModel;
                _ = viewModel.AddDominatorTraderModel(LatestSpreadGeneratorResults, GetTitle());
            }
        }

        public async void OpenInBasketTraderWithConfig(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    if (parameter is ConfigSave config)
                    {
                        var open = await GetConfirmation(config.Title);
                        if (open)
                        {
                            if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                            {
                                if (viewModel.IsReady)
                                {
                                    Task.Run(() => OnReady(viewModel));
                                }
                                else
                                {
                                    viewModel.Ready += OnReady;
                                }

                                async void OnReady(IModuleViewModel _)
                                {
                                    viewModel.Ready -= OnReady;
                                    ConfigSave configSave = config;
                                    configSave = await OmsCore.GatewayClient.RequestConfigDataAsync(configSave.Id);
                                    view.RestoreFromConfigSave(configSave);
                                    viewModel.LoadFromSpreadResultsAsync(LatestSpreadGeneratorResults.ToList());
                                }
                            }
                        }
                    }
                    else
                    {
                        await OpenInBasketTrader();
                    }
                }

            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTraderWithConfig));
            }
        }

        [Command]
        public async Task OpenInBasketTrader()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
                {
                    var open = await GetConfirmation();
                    if (open)
                    {
                        if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                        {
                            if (viewModel.IsReady)
                            {
                                Task.Run(() => OnReady(viewModel));
                            }
                            else
                            {
                                viewModel.Ready += OnReady;
                            }

                            void OnReady(IModuleViewModel module)
                            {
                                module.Ready -= OnReady;
                                var sample = LatestSpreadGeneratorResults.FirstOrDefault();
                                if (sample != null)
                                {
                                    var isSingleLeg = sample.Strategy == Strategy.SingleLeg;
                                    var underlying = sample.Underlying;
                                    view.LoadDefaultConfig(underlying, isSingleLeg, sample.Strategy);
                                }
                                LoadBasket(module);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private async void OpenInLockTrader()
        {
            try
            {
                var open = await GetConfirmation("lock");
                if (open)
                {
                    if (_moduleFactory.CreateModule(Module.LockTrader) is LockTraderView { ViewModel: LockTraderViewModel viewModel })
                    {
                        if (viewModel.IsReady)
                        {
                            LoadBasket(viewModel);
                        }
                        else
                        {
                            viewModel.Ready += LoadBasket;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInBasketTrader));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private async Task<bool> GetConfirmation(string configTitle = "")
        {
            var count = LatestSpreadGeneratorResults.Sum(x => x.Spreads.Count);
            if (count < OmsCore.Config.SpreadGeneratorPromptLimit)
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(configTitle))
            {
                configTitle = configTitle.Trim() + " ";
            }
            MessageResult result = MessageResult.No;
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                result = MessageBoxService.Show($"Would you like to load {count} spreads in {configTitle}basket?",
                    "Spreads Generator",
                    MessageButton.YesNo,
                    MessageIcon.Question,
                    MessageResult.No);
            }));
            return result == MessageResult.Yes;
        }

        private async void LoadBasket(IModuleViewModel module)
        {
            module.Ready -= LoadBasket;
            if (module is BasketTraderViewModel basket)
            {
                await basket.LoadFromSpreadResultsAsync(LatestSpreadGeneratorResults.ToList());
            }
        }

        [Command]
        public void SaveToFile()
        {
            try
            {
                ExportSpreadsToFileView exportToFileView = new()
                {
                    DataContext = this
                };
                exportToFileView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveToFile));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
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
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public async Task WriteToFile()
        {
            try
            {
                string titleString = GetTitle(!CrateSeparateExportForUnderlyings);
                if (titleString.Length > 230)
                {
                    titleString = titleString.Substring(0, 230);
                }
                bool save;
                switch (ExportFormat.ToUpper())
                {
                    case "DOMINATOR LIST":
                        titleString = "DOMINATOR SPREADS " + titleString;
                        if (titleString.Length > 230)
                        {
                            titleString = titleString.Substring(0, 230);
                        }
                        SaveFileDialogService.DefaultExt = "xlsx";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm} - {LatestSpreadGeneratorResults.Sum(x => x.Spreads.Count)} spreads";
                        SaveFileDialogService.Filter = "Dominator List|*.XLSX";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string filePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await WriteSpreadsToFileUsingDominatorFormat(filePath);
                            }
                            else
                            {
                                SaveWhenDone = true;
                            }
                        }
                        break;
                    case "CSV":
                        SaveFileDialogService.DefaultExt = "csv";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm}";
                        SaveFileDialogService.Filter = "Comma Separated Values|*.CSV";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string filePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await Task.Run(() => WriteSpreadsToFile(filePath));
                            }
                            else
                            {
                                SaveWhenDone = true;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(WriteToFile));
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        [Command]
        public async Task GenerateSpreads()
        {
            try
            {
                Generating = true;
                AllowGenerating = false;
                CanExport = false;
                ShowProgressBar = true;
                ProgressStatus = "Generating " + GetTitle();
                Stopwatch stopwatch = Stopwatch.StartNew();
                LatestSpreadGeneratorResults = new();
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;

                UpdateGlobalSettings();

                List<SpreadGeneratorResults> spreadGeneratorResults = RunnerOption == RunnerOption.Server
                    ? await GenerateOnServer(token)
                    : await GenerateLocally(token);

                HashSet<string> errors = spreadGeneratorResults.SelectMany(x => x.Errors).ToHashSet();
                int totalCount = spreadGeneratorResults.Sum(x => x.Spreads.Count);

                if (totalCount > 0)
                {
                    LatestSpreadGeneratorResults = spreadGeneratorResults.ToObservableCollection();
                    CanExport = true;
                    if (SaveWhenDone)
                    {
                        _ = WriteToFileWhenDone();
                    }
                }
                else
                {
                    CanExport = false;
                }

                ShowProgressBar = false;
                ProgressStatus = $"Done! {totalCount:N0} spreads generated, in {stopwatch.ElapsedMilliseconds}ms.";
                stopwatch.Stop();

                if (errors.Count > 0)
                {
                    MessageResult result = MessageResult.No;
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        result = MessageBoxService.Show($"Spreads generator finished with {errors.Count} errors.\n" +
                                                        "Would you like view the errors?",
                            "Spreads Generator",
                            MessageButton.YesNo,
                            MessageIcon.Question,
                            MessageResult.No);
                    }));
                    if (result == MessageResult.Yes)
                    {
                        await Dispatcher.BeginInvoke(new Action(() =>
                            MessageBoxService.ShowMessage($"{String.Join("\n", errors)}",
                                "Spreads Generator Errors",
                                MessageButton.OK,
                                MessageIcon.Error)
                        ));
                    }
                }
            }
            catch (AggregateException ae)
            {
                if (ae.InnerExceptions.Any(x => x is TaskCanceledException))
                {
                    ShowProgressBar = false;
                    ProgressStatus = "Operation cancelled!";
                }
                else
                {
                    _log.Error(ae, nameof(GenerateSpreads));

                    ShowProgressBar = false;
                    ProgressStatus = $"Error! {ae.Message}.";

                    _ = Dispatcher?.BeginInvoke(new Action(() =>
                        MessageBoxService.ShowMessage($"Something went wrong.\n{ae.Message}", "ZeroPlus OMS",
                            MessageButton.OK, MessageIcon.Error)
                    ));
                }
            }
            catch (TaskCanceledException)
            {
                ShowProgressBar = false;
                ProgressStatus = "Operation cancelled!";
            }
            catch (OperationCanceledException ex)
            {
                ShowProgressBar = false;
                ProgressStatus = $"{ex.Message}";
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateSpreads));

                ShowProgressBar = false;
                ProgressStatus = $"Error! {ex.Message}.";

                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
            finally
            {
                Generating = false;
                AllowGenerating = true;
            }
        }

        private async Task<List<SpreadGeneratorResults>> GenerateOnServer(CancellationToken token)
        {
            SpreadsGeneratorConfig config = GetConfig(withContent: true, activeOnly: true);
            List<SpreadGeneratorResults> spreadGeneratorResults = await OmsCore.SymbolMapClient.Client.GenerateSpreadsAsync(config, token);
            return spreadGeneratorResults;
        }

        private async Task<List<SpreadGeneratorResults>> GenerateLocally(CancellationToken token)
        {
            _bidStore.Reset();
            _askStore.Reset();
            _lastStore.Reset();
            _emaStore.Reset();
            _deltaStore.Reset();
            _vegaStore.Reset();
            _theoStore.Reset();
            _volaStore.Reset();

            _bidStore.ApproximateOnFailure = ApproximateMissingQuotes;
            _askStore.ApproximateOnFailure = ApproximateMissingQuotes;
            _lastStore.ApproximateOnFailure = ApproximateMissingQuotes;
            _emaStore.ApproximateOnFailure = ApproximateMissingQuotes;
            _deltaStore.ApproximateOnFailure = ApproximateMissingGreeks;
            _vegaStore.ApproximateOnFailure = ApproximateMissingGreeks;
            _theoStore.ApproximateOnFailure = ApproximateMissingHanweck;

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Task<SpreadGeneratorResults>> tasks = new();
            List<Task<List<Task<SpreadGeneratorResults>>>> masterTasks = new();
            int maxCount = int.MaxValue;

            if (ParsedOutputEnabled)
            {
                maxCount = ParsedBuildCount;
            }
            else if (MaxCountEnabled)
            {
                maxCount = MaxCount;
            }

            if (FreshSpreads)
            {
                _underlyingToFishedExpirationsMap.Clear();
                _buffer.Clear();
                _waitForOrderLoadEvent.Reset();
                DateTime startDateTime = DateTime.Today;
                DateTime endDateTime = DateTime.Now;
                List<string> underlyings = OptionChains.Select(x => x.Symbol).ToList();
                List<OrderStatus> orderStatus = new() { OrderStatus.New };
                int requestId = OmsCore.HerculesClient.RequestTransactionsFromArchive(startDateTime, endDateTime,
                    ordersOnly: true, orderStatus, new List<string>(), new List<string>(), new List<string>(),
                    underlyings);
                _transactionConsumerModel.AddRequester(requestId, this);
                await _waitForOrderLoadEvent.WaitOneAsync(CancellationToken.None);
            }

            foreach (OptionChainModel optionChain in OptionChains)
            {
                List<Option> mustCalendarHaveExpirations = null;
                if (CalendarEnabled && CalendarTargetExpirationListEnabled)
                {
                    HashSet<DateTime> target = CalendarTargetExpirationsList.Where(x => x.IsChecked)
                        .Select(x => x.Date.Date).ToHashSet();
                    mustCalendarHaveExpirations = optionChain.OptionChain
                        .Where(x => target.Contains(x.Expiration.Date)).ToList();
                }

                List<Option> mustDiagonalHaveExpirations = null;
                if (DiagonalEnabled && DiagonalTargetExpirationListEnabled)
                {
                    HashSet<DateTime> target = DiagonalTargetExpirationsList.Where(x => x.IsChecked)
                        .Select(x => x.Date.Date).ToHashSet();
                    mustDiagonalHaveExpirations = optionChain.OptionChain
                        .Where(x => target.Contains(x.Expiration.Date)).ToList();
                }

                List<Option> filteredChain = await FilterOptionChainByStrikeAndExpBoundAsync(optionChain);
                if (FreshSpreads)
                {
                    if (_underlyingToFishedExpirationsMap.TryGetValue(optionChain.Symbol,
                            out (HashSet<string> Puts, HashSet<string> Calls) fishedExpirations))
                    {
                        filteredChain = filteredChain.Where(x =>
                                (x.PutCall == PutCall.Put && !fishedExpirations.Puts.Contains(x.Symbol)) ||
                                (x.PutCall == PutCall.Call && !fishedExpirations.Calls.Contains(x.Symbol)))
                            .ToList();
                    }
                }

                List<Option> leg1Options = filteredChain;
                List<Option> leg2Options = filteredChain;
                List<Option> leg3Options = filteredChain;
                List<Option> leg4Options = filteredChain;

                if (Leg1LockEnabled)
                {
                    HashSet<string> leg1FilterOption =
                        (Leg1LockOptions ?? "").Split(",").Select(x => x.Trim()).ToHashSet();
                    leg1Options = filteredChain.Where(x => leg1FilterOption.Contains(x.Symbol)).ToList();
                }

                if (Leg2LockEnabled)
                {
                    HashSet<string> leg2FilterOption =
                        (Leg2LockOptions ?? "").Split(",").Select(x => x.Trim()).ToHashSet();
                    leg2Options = filteredChain.Where(x => leg2FilterOption.Contains(x.Symbol)).ToList();
                }

                if (Leg3LockEnabled)
                {
                    HashSet<string> leg3FilterOption =
                        (Leg3LockOptions ?? "").Split(",").Select(x => x.Trim()).ToHashSet();
                    leg3Options = filteredChain.Where(x => leg3FilterOption.Contains(x.Symbol)).ToList();
                }

                if (Leg4LockEnabled)
                {
                    HashSet<string> leg4FilterOption =
                        (Leg4LockOptions ?? "").Split(",").Select(x => x.Trim()).ToHashSet();
                    leg4Options = filteredChain.Where(x => leg4FilterOption.Contains(x.Symbol)).ToList();
                }

                if (Leg1OpenInterestFilter.Enabled ||
                    Leg2OpenInterestFilter.Enabled ||
                    Leg3OpenInterestFilter.Enabled ||
                    Leg4OpenInterestFilter.Enabled)
                {
                    if (Leg1OpenInterestFilter.Enabled)
                    {
                        leg1Options = new List<Option>();
                    }

                    if (Leg2OpenInterestFilter.Enabled)
                    {
                        leg2Options = new List<Option>();
                    }

                    if (Leg3OpenInterestFilter.Enabled)
                    {
                        leg3Options = new List<Option>();
                    }

                    if (Leg4OpenInterestFilter.Enabled)
                    {
                        leg4Options = new List<Option>();
                    }

                    DataStore openInterestStore = new(token, OmsCore.Config.SpreadGeneratorTimeout,
                        OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                    openInterestStore.GetQuoteDataFor(filteredChain, SubscriptionFieldType.OpenInterest);

                    foreach (Option option in filteredChain)
                    {
                        token.ThrowIfCancellationRequested();
                        double openInterest = await openInterestStore.GetDataAsync(option.Symbol);

                        if (Leg1OpenInterestFilter.Enabled && ApplyFilter(Leg1OpenInterestFilter.Filter,
                                openInterest, Leg1OpenInterestFilter.Value))
                        {
                            leg1Options.Add(option);
                        }

                        if (Leg2OpenInterestFilter.Enabled && ApplyFilter(Leg2OpenInterestFilter.Filter,
                                openInterest, Leg2OpenInterestFilter.Value))
                        {
                            leg2Options.Add(option);
                        }

                        if (Leg3OpenInterestFilter.Enabled && ApplyFilter(Leg3OpenInterestFilter.Filter,
                                openInterest, Leg3OpenInterestFilter.Value))
                        {
                            leg3Options.Add(option);
                        }

                        if (Leg4OpenInterestFilter.Enabled && ApplyFilter(Leg4OpenInterestFilter.Filter,
                                openInterest, Leg4OpenInterestFilter.Value))
                        {
                            leg4Options.Add(option);
                        }
                    }
                }


                if (Leg1VolumeFilter.Enabled ||
                    Leg2VolumeFilter.Enabled ||
                    Leg3VolumeFilter.Enabled ||
                    Leg4VolumeFilter.Enabled)
                {
                    DataStore volumeStore = new(token, OmsCore.Config.SpreadGeneratorTimeout,
                        OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                    volumeStore.GetQuoteDataFor(filteredChain, SubscriptionFieldType.Volume);

                    if (Leg1VolumeFilter.Enabled)
                    {
                        List<Option> selected = new();
                        foreach (Option leg1 in leg1Options)
                        {
                            token.ThrowIfCancellationRequested();
                            double volume = await volumeStore.GetDataAsync(leg1.Symbol);
                            if (ApplyFilter(Leg1VolumeFilter.Filter, volume, Leg1VolumeFilter.Value))
                            {
                                selected.Add(leg1);
                            }
                        }

                        leg1Options = selected;
                    }

                    if (Leg2VolumeFilter.Enabled)
                    {
                        List<Option> selected = new();
                        foreach (Option leg2 in leg2Options)
                        {
                            token.ThrowIfCancellationRequested();
                            double volume = await volumeStore.GetDataAsync(leg2.Symbol);
                            if (ApplyFilter(Leg2VolumeFilter.Filter, volume, Leg2VolumeFilter.Value))
                            {
                                selected.Add(leg2);
                            }
                        }

                        leg2Options = selected;
                    }

                    if (Leg3VolumeFilter.Enabled)
                    {
                        List<Option> selected = new();
                        foreach (Option leg3 in leg3Options)
                        {
                            token.ThrowIfCancellationRequested();
                            double volume = await volumeStore.GetDataAsync(leg3.Symbol);
                            if (ApplyFilter(Leg3VolumeFilter.Filter, volume, Leg3VolumeFilter.Value))
                            {
                                selected.Add(leg3);
                            }
                        }

                        leg3Options = selected;
                    }

                    if (Leg4VolumeFilter.Enabled)
                    {
                        List<Option> selected = new();
                        foreach (Option leg4 in leg4Options)
                        {
                            token.ThrowIfCancellationRequested();
                            double volume = await volumeStore.GetDataAsync(leg4.Symbol);
                            if (ApplyFilter(Leg4VolumeFilter.Filter, volume, Leg4VolumeFilter.Value))
                            {
                                selected.Add(leg4);
                            }
                        }

                        leg4Options = selected;
                    }
                }

                List<Option> leg1CallOptionsChain = leg1Options.Where(x => x.PutCall == PutCall.Call).ToList();
                List<Option> leg1PutOptionsChain = leg1Options.Where(x => x.PutCall == PutCall.Put).ToList();

                List<Option> leg2CallOptionsChain = leg2Options.Where(x => x.PutCall == PutCall.Call).ToList();
                List<Option> leg2PutOptionsChain = leg2Options.Where(x => x.PutCall == PutCall.Put).ToList();

                List<Option> leg3CallOptionsChain = leg3Options.Where(x => x.PutCall == PutCall.Call).ToList();
                List<Option> leg3PutOptionsChain = leg3Options.Where(x => x.PutCall == PutCall.Put).ToList();

                List<Option> leg4CallOptionsChain = leg4Options.Where(x => x.PutCall == PutCall.Call).ToList();
                List<Option> leg4PutOptionsChain = leg4Options.Where(x => x.PutCall == PutCall.Put).ToList();

                if (SingleLegEnabled)
                {
                    List<Option> callOptionsChain = leg1CallOptionsChain;
                    List<Option> putOptionsChain = leg1PutOptionsChain;

                    List<Option> callOptionsToInclude = null;
                    List<Option> putOptionsToInclude = null;

                    if (StrikeIncludeFromTopAndBottomEnabled &&
                        StrikeIncludeFromTopAndBottomCount > 0)
                    {
                        callOptionsToInclude = new();
                        putOptionsToInclude = new();

                        IEnumerable<IGrouping<DateTime, Option>> callsGrouped =
                            callOptionsChain.GroupBy(x => x.Expiration);
                        foreach (IGrouping<DateTime, Option> expGroup in callsGrouped)
                        {
                            IEnumerable<Option> top = expGroup.OrderBy(x => x.Strike)
                                .Take(StrikeIncludeFromTopAndBottomCount);
                            IEnumerable<Option> bottom = expGroup.OrderByDescending(x => x.Strike)
                                .Take(StrikeIncludeFromTopAndBottomCount);
                            callOptionsToInclude.AddRange(top);
                            callOptionsToInclude.AddRange(bottom);
                        }

                        IEnumerable<IGrouping<DateTime, Option>> putsGrouped =
                            putOptionsChain.GroupBy(x => x.Expiration);
                        foreach (IGrouping<DateTime, Option> expGroup in putsGrouped)
                        {
                            IEnumerable<Option> top = expGroup.OrderBy(x => x.Strike)
                                .Take(StrikeIncludeFromTopAndBottomCount);
                            IEnumerable<Option> bottom = expGroup.OrderByDescending(x => x.Strike)
                                .Take(StrikeIncludeFromTopAndBottomCount);
                            putOptionsToInclude.AddRange(top);
                            putOptionsToInclude.AddRange(bottom);
                        }
                    }

                    if (SingleLegSpreadsSettings.ExcludedTradedSymbols)
                    {
                        callOptionsChain = _portfolioManager.GetNonTradedByFirm(leg1CallOptionsChain);
                        putOptionsChain = _portfolioManager.GetNonTradedByFirm(leg1PutOptionsChain);
                    }

                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => SingleLegSpreadsGenerator.GenerateAsync(callOptionsChain,
                            callOptionsToInclude,
                            StrikeIncludeAsAdditionFromTopAndBottom,
                            SingleLegSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => SingleLegSpreadsGenerator.GenerateAsync(putOptionsChain,
                            putOptionsToInclude,
                            StrikeIncludeAsAdditionFromTopAndBottom,
                            SingleLegSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (VerticalEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => VerticalSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            VerticalSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => VerticalSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            VerticalSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (OneByTwoRatioEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            OneByTwoRatioSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            OneByTwoRatioSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (OneByThreeRatioEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            OneByThreeRatioSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            OneByThreeRatioSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (RatioEnabled)
                {
                    RatioSpreadsSettings.SetRatio();
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            RatioSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            RatioSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (CalendarEnabled)
                {
                    if (CallsEnabled)
                    {
                        if (mustCalendarHaveExpirations != null && mustCalendarHaveExpirations.Count > 0)
                        {
                            List<Option> mustHaveList = mustCalendarHaveExpirations
                                .Where(x => x.PutCall == PutCall.Call).ToList();
                            tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(mustHaveList,
                                leg2CallOptionsChain,
                                CalendarSpreadsSettings,
                                maxCount / 2,
                                token), token));
                            tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                mustHaveList,
                                CalendarSpreadsSettings,
                                maxCount / 2,
                                token), token));
                        }
                        else
                        {
                            tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                leg2CallOptionsChain,
                                CalendarSpreadsSettings,
                                maxCount,
                                token), token));
                        }
                    }

                    if (PutsEnabled)
                    {
                        if (mustCalendarHaveExpirations != null && mustCalendarHaveExpirations.Count > 0)
                        {
                            List<Option> mustHaveList = mustCalendarHaveExpirations
                                .Where(x => x.PutCall == PutCall.Put).ToList();
                            tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(mustHaveList,
                                leg2PutOptionsChain,
                                CalendarSpreadsSettings,
                                maxCount / 2,
                                token), token));
                            tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                mustHaveList,
                                CalendarSpreadsSettings,
                                maxCount / 2,
                                token), token));
                        }
                        else
                        {

                            tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                leg2PutOptionsChain,
                                CalendarSpreadsSettings,
                                maxCount,
                                token), token));
                        }
                    }
                }

                if (DiagonalEnabled)
                {
                    if (CallsEnabled)
                    {
                        if (mustDiagonalHaveExpirations != null && mustDiagonalHaveExpirations.Count > 0)
                        {
                            List<Option> mustHaveList = mustDiagonalHaveExpirations
                                .Where(x => x.PutCall == PutCall.Call).ToList();
                            tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(mustHaveList,
                                leg2CallOptionsChain,
                                DiagonalSpreadsSettings,
                                maxCount / 2,
                                token), token));
                            tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                mustHaveList,
                                DiagonalSpreadsSettings,
                                maxCount / 2,
                                token), token));
                        }
                        else
                        {
                            tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                leg2CallOptionsChain,
                                DiagonalSpreadsSettings,
                                maxCount,
                                token), token));
                        }
                    }

                    if (PutsEnabled)
                    {
                        if (mustDiagonalHaveExpirations != null && mustDiagonalHaveExpirations.Count > 0)
                        {
                            List<Option> mustHaveList = mustDiagonalHaveExpirations
                                .Where(x => x.PutCall == PutCall.Put).ToList();
                            tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(mustHaveList,
                                leg2PutOptionsChain,
                                DiagonalSpreadsSettings,
                                maxCount / 2,
                                token), token));
                            tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                mustHaveList,
                                DiagonalSpreadsSettings,
                                maxCount / 2,
                                token), token));
                        }
                        else
                        {
                            tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                leg2PutOptionsChain,
                                DiagonalSpreadsSettings,
                                maxCount,
                                token), token));
                        }
                    }
                }

                if (ButterflyEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => ButterflySpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            ButterflySpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => ButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            ButterflySpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (SkewedButterflyEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => SkewedButterflySpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            SkewedButterflySpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => SkewedButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            SkewedButterflySpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (TreeEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => TreeSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            TreeSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => TreeSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            TreeSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (CalendarButterflyEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => CalendarButterflySpreadsGenerator.GenerateAsync(
                            leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            CalendarButterflySpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => CalendarButterflySpreadsGenerator.GenerateAsync(
                            leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            CalendarButterflySpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (IronButterflyEnabled)
                {
                    tasks.Add(Task.Run(() => IronButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                        leg2PutOptionsChain,
                        leg3CallOptionsChain,
                        leg4CallOptionsChain,
                        IronButterflySpreadsSettings,
                        maxCount,
                        token), token));
                }

                if (IronGutFlyEnabled)
                {
                    tasks.Add(Task.Run(() => IronGutFlyGenerator.GenerateAsync(leg1CallOptionsChain,
                        leg2CallOptionsChain,
                        leg3PutOptionsChain,
                        leg3PutOptionsChain,
                        IronGutFlySpreadsSettings,
                        maxCount,
                        token), token));
                }

                if (CondorEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => CondorSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            leg4CallOptionsChain,
                            CondorSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => CondorSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            leg4PutOptionsChain,
                            CondorSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (IronCondorEnabled)
                {
                    tasks.Add(Task.Run(() => IronCondorSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                        leg2PutOptionsChain,
                        leg3CallOptionsChain,
                        leg4CallOptionsChain,
                        IronCondorSpreadsSettings,
                        maxCount,
                        token), token));
                }

                if (OneThreeThreeOneEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => OneThreeThreeOneSpreadsGenerator.GenerateAsync(
                            leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            leg4CallOptionsChain,
                            OneThreeThreeOneSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => OneThreeThreeOneSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            leg4PutOptionsChain,
                            OneThreeThreeOneSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (OneThreeTwoEnabled)
                {
                    if (CallsEnabled)
                    {
                        await OneThreeTwoSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            OneThreeTwoSpreadsSettings,
                            maxCount,
                            token);
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => OneThreeTwoSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            OneThreeTwoSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (TwoThreeOneEnabled)
                {
                    if (CallsEnabled)
                    {
                        tasks.Add(Task.Run(() => TwoThreeOneSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                            leg2CallOptionsChain,
                            leg3CallOptionsChain,
                            TwoThreeOneSpreadsSettings,
                            maxCount,
                            token), token));
                    }

                    if (PutsEnabled)
                    {
                        tasks.Add(Task.Run(() => TwoThreeOneSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                            leg2PutOptionsChain,
                            leg3PutOptionsChain,
                            TwoThreeOneSpreadsSettings,
                            maxCount,
                            token), token));
                    }
                }

                if (BoxEnabled)
                {
                    tasks.Add(Task.Run(() => BoxSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                        leg2CallOptionsChain,
                        leg3PutOptionsChain,
                        leg4PutOptionsChain,
                        BoxSpreadsGeneratorSettings,
                        maxCount,
                        token), token));
                }

                if (ConfigChainEnabled)
                {
                    foreach (ConfigChainModel config in ConfigChains)
                    {
                        if (config.Config != null)
                        {
                            masterTasks.Add(Task.Run(() => GenerateFromTemplate(config.Config, leg1CallOptionsChain,
                                leg2CallOptionsChain, leg3CallOptionsChain, leg4CallOptionsChain,
                                leg1PutOptionsChain, leg2PutOptionsChain, leg3PutOptionsChain, leg4PutOptionsChain,
                                maxCount, token)));
                        }
                    }
                }
            }

            masterTasks.Add(Task.Run(() => tasks, token));

            List<SpreadGeneratorResults> spreadGeneratorResults = await Task.Run(
                () => Task.WhenAll(masterTasks).ContinueWith(_ =>
                    masterTasks.SelectMany(task => task.Result.Select(x => x!.Result)).ToList(), token), token);
            HashSet<string> errors = spreadGeneratorResults.SelectMany(x => x.Errors).ToHashSet();

            int totalCount = spreadGeneratorResults.Sum(x => x.Spreads.Count);

            if (ParsedOutputEnabled)
            {
                ProgressStatus =
                    $"Pre-parse done! {totalCount:N0} spreads generated, in {stopwatch.ElapsedMilliseconds}ms.";
            }
            else if (MaxCountEnabled)
            {
                spreadGeneratorResults =
                    await AdjustForMaxCountQuota(spreadGeneratorResults.ToList(), GetMaxCount(), token);
                totalCount = spreadGeneratorResults.Sum(x => x.Spreads.Count);
            }

            if (totalCount > 0)
            {
                spreadGeneratorResults.ForEach(x => x.UpdateExpirations());
                List<SpreadGeneratorResults> results =
                    spreadGeneratorResults.Where(x => x.Underlying != null).ToList();

                if (ApplyDteToExpSpacingMap)
                {
                    ApplyDteToExpSpacingMapToResults(results);
                    totalCount = results.Sum(x => x.Spreads.Count);
                }

                if (ParsedOutputEnabled && results.Count > 0)
                {
                    LatestSpreadGeneratorResults ??= new();
                    foreach (var spreadGeneratorResult in spreadGeneratorResults)
                    {
                        LatestSpreadGeneratorResults.Add(spreadGeneratorResult);
                    }
                    ParserFrontMonthPercent = 1;
                    ParserBackMonthPercent = 1;
                    ParserGroupTotalCount = ParsedOutputCount / results.Count;
                    ParserMinCount = Math.Min(ParserGroupTotalCount, 20);
                    ParserParameterChangedCommand();
                    await RunParserCommand();
                    totalCount = results.Sum(x => x.Spreads.Count);
                    if (totalCount > ParsedOutputCount)
                    {
                        return await AdjustForMaxCountQuota(LatestSpreadGeneratorResults.ToList(), ParserGroupTotalCount, token);
                    }
                }

                if (TopPercentageSelectionEnabled)
                {
                    var targetCount = (int)Math.Ceiling(totalCount * TopPercentageSelectionCount);
                    HashSet<Spread> selectedSpreads = null;
                    if (TopPercentageSelectionSortingMode == OutputSortingMode.HighestWidth)
                    {
                        selectedSpreads = results
                            .SelectMany(x => x.Spreads)
                            .OrderByDescending(x => x.Width)
                            .Take(targetCount)
                            .ToHashSet();
                    }
                    else if (TopPercentageSelectionSortingMode == OutputSortingMode.LowestWidth)
                    {
                        selectedSpreads = results
                            .SelectMany(x => x.Spreads)
                            .OrderBy(x => x.Width)
                            .Take(targetCount)
                            .ToHashSet();
                    }

                    if (selectedSpreads != null)
                    {
                        foreach (var result in results)
                        {
                            result.Spreads.RemoveWhere(spread => !selectedSpreads.Contains(spread));
                        }
                    }
                }

                return results;
            }
            else
            {
                CanExport = false;
            }

            return spreadGeneratorResults;
        }

        private void UpdateGlobalSettings()
        {
            foreach (ISpreadsGeneratorSettings setting in SpreadGeneratorSettings)
            {
                setting.WidthSortingEnabled = TopPercentageSelectionEnabled && TopPercentageSelectionSortingMode is OutputSortingMode.LowestWidth or OutputSortingMode.HighestWidth;
            }
        }

        private static void ApplyDteToExpSpacingMapToResults(List<SpreadGeneratorResults> results)
        {
            try
            {
                foreach (SpreadGeneratorResults result in results)
                {
                    if (result.Strategy is Strategy.Calendar or
                        Strategy.Diagonal)
                    {
                        foreach (Spread spread in result.Spreads.ToList())
                        {
                            var option = spread.Legs[0].Option;

                            if (option == null)
                            {
                                continue;
                            }

                            Option nearLeg = option.Expiration < spread.Legs[1].Option.Expiration ? option : spread.Legs[1].Option;
                            Option farLeg = option.Expiration > spread.Legs[1].Option.Expiration ? option : spread.Legs[1].Option;
                            if (nearLeg != null &&
                                farLeg != null &&
                                nearLeg.Expiration >= DateTime.Today &&
                                farLeg.Expiration >= DateTime.Today &&
                                nearLeg.Expiration != farLeg.Expiration)
                            {
                                double nearLegDte = (nearLeg.Expiration - DateTime.Today).TotalDays;
                                double farLegDte = (farLeg.Expiration - DateTime.Today).TotalDays;
                                double expSpacing = (farLeg.Expiration - nearLeg.Expiration).TotalDays;

                                if (nearLegDte < 40)
                                {
                                    if (expSpacing > 55)
                                    {
                                        if (expSpacing > nearLegDte * 2.5)
                                        {
                                            result.Spreads.Remove(spread);
                                        }
                                    }
                                }
                                else if (nearLegDte is >= 40 and < 100)
                                {
                                    if (expSpacing > nearLegDte * 1.3)
                                    {
                                        result.Spreads.Remove(spread);
                                    }
                                }
                                else if (nearLegDte is >= 100 and < 330)
                                {
                                    if (expSpacing > nearLegDte * .7)
                                    {
                                        result.Spreads.Remove(spread);
                                    }
                                }
                                else if (nearLegDte >= 330)
                                {
                                    if (expSpacing > nearLegDte * 1.25)
                                    {
                                        result.Spreads.Remove(spread);
                                    }
                                }
                            }
                        }
                    }
                }
                results.ForEach(x => x.UpdateExpirations());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ApplyDteToExpSpacingMapToResults));
            }
        }

        private async Task<List<Task<SpreadGeneratorResults>>> GenerateFromTemplate(ConfigSave config, List<Option> leg1CallOptionsChain, List<Option> leg2CallOptionsChain, List<Option> leg3CallOptionsChain, List<Option> leg4CallOptionsChain, List<Option> leg1PutOptionsChain, List<Option> leg2PutOptionsChain, List<Option> leg3PutOptionsChain, List<Option> leg4PutOptionsChain, int maxCount, CancellationToken token)
        {
            SpreadsGeneratorConfig template = await OmsCore.GatewayClient.RequestConfigDataAsync(config.Id).ContinueWith(t => JsonConvert.DeserializeObject<SpreadsGeneratorConfig>(t.Result.ConfigJson, SpreadGeneratorConfigSerializationSettings), token);

            List<Task<SpreadGeneratorResults>> tasks = new();

            if (template.SingleLegEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => SingleLegSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                     null,
                                                                                     false,
                                                                                     template.SingleLegSpreadsSettings!,
                                                                                     maxCount,
                                                                                     token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => SingleLegSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                     null,
                                                                                     false,
                                                                                     template.SingleLegSpreadsSettings!,
                                                                                     maxCount,
                                                                                     token), token));
                }
            }

            if (template.VerticalEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => VerticalSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                    leg2CallOptionsChain,
                                                                                    template.VerticalSpreadsSettings!,
                                                                                    maxCount,
                                                                                    token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => VerticalSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                    leg2PutOptionsChain,
                                                                                    template.VerticalSpreadsSettings!,
                                                                                    maxCount,
                                                                                    token), token));
                }
            }

            if (template.OneByTwoRatioEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                 leg2CallOptionsChain,
                                                                                 template.OneByTwoRatioSpreadsSettings!,
                                                                                 maxCount,
                                                                                 token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                 leg2PutOptionsChain,
                                                                                 template.OneByTwoRatioSpreadsSettings!,
                                                                                 maxCount,
                                                                                 token), token));
                }
            }

            if (template.OneByThreeRatioEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                 leg2CallOptionsChain,
                                                                                 template.OneByThreeRatioSpreadsSettings!,
                                                                                 maxCount,
                                                                                 token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                 leg2PutOptionsChain,
                                                                                 template.OneByThreeRatioSpreadsSettings!,
                                                                                 maxCount,
                                                                                 token), token));
                }
            }

            if (template.RatioEnabled)
            {
                RatioSpreadsSettings.SetRatio();
                if (CallsEnabled)
                {
                    tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                 leg2CallOptionsChain,
                                                                                 template.RatioSpreadsSettings!,
                                                                                 maxCount,
                                                                                 token), token));
                }

                if (PutsEnabled)
                {
                    tasks.Add(Task.Run(() => RatioSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                 leg2PutOptionsChain,
                                                                                 template.RatioSpreadsSettings!,
                                                                                 maxCount,
                                                                                 token), token));
                }
            }

            if (template.CalendarEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                    leg2CallOptionsChain,
                                                                                    template.CalendarSpreadsSettings!,
                                                                                    maxCount,
                                                                                    token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                    leg2PutOptionsChain,
                                                                                    template.CalendarSpreadsSettings!,
                                                                                    maxCount,
                                                                                    token), token));
                }
            }

            if (template.DiagonalEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                    leg2CallOptionsChain,
                                                                                    template.DiagonalSpreadsSettings!,
                                                                                    maxCount,
                                                                                    token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                    leg2PutOptionsChain,
                                                                                    template.DiagonalSpreadsSettings!,
                                                                                    maxCount,
                                                                                    token), token));
                }
            }

            if (template.ButterflyEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => ButterflySpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                     leg2CallOptionsChain,
                                                                                     leg3CallOptionsChain,
                                                                                     template.ButterflySpreadsSettings!,
                                                                                     maxCount,
                                                                                     token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => ButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                     leg2PutOptionsChain,
                                                                                     leg3PutOptionsChain,
                                                                                     template.ButterflySpreadsSettings!,
                                                                                     maxCount,
                                                                                     token), token));
                }
            }

            if (template.SkewedButterflyEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => SkewedButterflySpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                           leg2CallOptionsChain,
                                                                                           leg3CallOptionsChain,
                                                                                           template.SkewedButterflySpreadsSettings!,
                                                                                           maxCount,
                                                                                           token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => SkewedButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                           leg2PutOptionsChain,
                                                                                           leg3PutOptionsChain,
                                                                                           template.SkewedButterflySpreadsSettings!,
                                                                                           maxCount,
                                                                                           token), token));
                }
            }

            if (template.TreeEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => TreeSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                           leg2CallOptionsChain,
                                                                                           leg3CallOptionsChain,
                                                                                           template.TreeSpreadsSettings!,
                                                                                           maxCount,
                                                                                           token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => TreeSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                           leg2PutOptionsChain,
                                                                                           leg3PutOptionsChain,
                                                                                           template.TreeSpreadsSettings!,
                                                                                           maxCount,
                                                                                           token), token));
                }
            }

            if (template.CalendarButterflyEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => CalendarButterflySpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                           leg2CallOptionsChain,
                                                                                           leg3CallOptionsChain,
                                                                                           template.CalendarButterflySpreadsSettings!,
                                                                                           maxCount,
                                                                                           token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => CalendarButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                           leg2PutOptionsChain,
                                                                                           leg3PutOptionsChain,
                                                                                           template.CalendarButterflySpreadsSettings!,
                                                                                           maxCount,
                                                                                           token), token));
                }
            }

            if (template.IronButterflyEnabled)
            {
                tasks.Add(Task.Run(() => IronButterflySpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                     leg2PutOptionsChain,
                                                                                     leg3CallOptionsChain,
                                                                                     leg4CallOptionsChain,
                                                                                     template.IronButterflySpreadsSettings!,
                                                                                     maxCount,
                                                                                     token), token));
            }

            if (template.IronGutFlyEnabled)
            {
                tasks.Add(Task.Run(() => IronGutFlyGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                           leg2CallOptionsChain,
                                                                           leg3PutOptionsChain,
                                                                           leg4PutOptionsChain,
                                                                           template.IronGutFlySpreadsSettings!,
                                                                           maxCount,
                                                                           token), token));
            }

            if (template.CondorEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => CondorSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                  leg2CallOptionsChain,
                                                                                  leg3CallOptionsChain,
                                                                                  leg4CallOptionsChain,
                                                                                  template.CondorSpreadsSettings!,
                                                                                  maxCount,
                                                                                  token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => CondorSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                  leg2PutOptionsChain,
                                                                                  leg3PutOptionsChain,
                                                                                  leg4PutOptionsChain,
                                                                                  template.CondorSpreadsSettings!,
                                                                                  maxCount,
                                                                                  token), token));
                }
            }

            if (template.IronCondorEnabled)
            {
                tasks.Add(Task.Run(() => IronCondorSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                  leg2PutOptionsChain,
                                                                                  leg3CallOptionsChain,
                                                                                  leg4CallOptionsChain,
                                                                                  template.IronCondorSpreadsSettings!,
                                                                                  maxCount,
                                                                                  token), token));
            }

            if (template.OneThreeThreeOneEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => OneThreeThreeOneSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                                                                                            leg2CallOptionsChain,
                                                                                            leg3CallOptionsChain,
                                                                                            leg4CallOptionsChain,
                                                                                            template.OneThreeThreeOneSpreadsSettings!,
                                                                                            maxCount,
                                                                                            token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => OneThreeThreeOneSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                                                                                            leg2PutOptionsChain,
                                                                                            leg3PutOptionsChain,
                                                                                            leg4PutOptionsChain,
                                                                                            template.OneThreeThreeOneSpreadsSettings!,
                                                                                            maxCount,
                                                                                            token), token));
                }
            }

            if (template.OneThreeTwoEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => OneThreeTwoSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                        leg2CallOptionsChain,
                        leg3CallOptionsChain,
                        template.OneThreeTwoSpreadsSettings!,
                        maxCount,
                        token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => OneThreeTwoSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                        leg2PutOptionsChain,
                        leg3PutOptionsChain,
                        template.OneThreeTwoSpreadsSettings!,
                        maxCount,
                        token), token));
                }
            }

            if (template.TwoThreeOneEnabled)
            {
                if (template.CallsEnabled)
                {
                    tasks.Add(Task.Run(() => TwoThreeOneSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                        leg2CallOptionsChain,
                        leg3CallOptionsChain,
                        template.TwoThreeOneSpreadsSettings!,
                        maxCount,
                        token), token));
                }

                if (template.PutsEnabled)
                {
                    tasks.Add(Task.Run(() => TwoThreeOneSpreadsGenerator.GenerateAsync(leg1PutOptionsChain,
                        leg2PutOptionsChain,
                        leg3PutOptionsChain,
                        template.TwoThreeOneSpreadsSettings!,
                        maxCount,
                        token), token));
                }
            }

            if (template.BoxEnabled)
            {
                tasks.Add(Task.Run(() => BoxSpreadsGenerator.GenerateAsync(leg1CallOptionsChain,
                    leg2CallOptionsChain,
                    leg3PutOptionsChain,
                    leg4PutOptionsChain,
                    template.BoxSpreadsSettings!,
                    maxCount,
                    token), token));
            }

            return tasks;
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
        public void ParserParameterChangedCommand()
        {
            foreach (SpreadGeneratorResults spreadGeneratorResult in LatestSpreadGeneratorResults)
            {
                if (spreadGeneratorResult != null)
                {
                    spreadGeneratorResult.MinCount = ParserMinCount;
                    spreadGeneratorResult.TotalCount = ParserGroupTotalCount;
                    spreadGeneratorResult.BackMonthExpirationsPercent = ParserBackMonthPercent;
                    spreadGeneratorResult.FrontMonthExpirationsPercent = ParserFrontMonthPercent;
                    spreadGeneratorResult.UpdateExpirationPercentage();
                }
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
                int totalCount = LatestSpreadGeneratorResults.Sum(x => x.Spreads.Count);
                stopwatch.Stop();
                ShowProgressBar = false;
                ProgressStatus = $"Done! {totalCount:N0} spreads parsed, in {stopwatch.ElapsedMilliseconds}ms.";
            }
            catch (TaskCanceledException)
            {
                int totalCount = LatestSpreadGeneratorResults.Sum(x => x.Spreads.Count);
                stopwatch.Stop();
                ShowProgressBar = false;
                ProgressStatus = $"Operation Cancelled! {totalCount:N0} spreads parsed, in {stopwatch.ElapsedMilliseconds}ms.";
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationPercentChangedCommand));
            }
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

        private static bool ApplyFilter(char filter, double comperand, double value)
        {
            return filter switch
            {
                '>' => comperand > value,
                '≥' => comperand >= value,
                '<' => comperand < value,
                '≤' => comperand <= value,
                '=' => comperand == value,
                _ => false,
            };
        }

        private static async Task<List<SpreadGeneratorResults>> AdjustForMaxCountQuota(List<SpreadGeneratorResults> spreadGeneratorResults, int groupQuota, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                List<SpreadGeneratorResults> finalResults = new();

                while (spreadGeneratorResults.Count > 0)
                {
                    List<SpreadGeneratorResults> resultsThatFellShortOfQuota = spreadGeneratorResults.Where(x => x.Spreads.Count <= groupQuota).ToList();

                    foreach (SpreadGeneratorResults result in resultsThatFellShortOfQuota)
                    {
                        finalResults.Add(result);
                        spreadGeneratorResults.Remove(result);
                    }

                    int missing = resultsThatFellShortOfQuota.Sum(x => groupQuota - x.Spreads.Count);
                    if (spreadGeneratorResults.Count == 0)
                    {
                        break;
                    }
                    else if (missing > 0)
                    {
                        groupQuota += missing / spreadGeneratorResults.Count;
                    }
                    else
                    {
                        for (int i = 0; i < spreadGeneratorResults.Count; i++)
                        {
                            finalResults.Add(spreadGeneratorResults[i].Select(groupQuota, token));
                        }
                        break;
                    }
                }

                return finalResults;
            });
        }

        private int GetMaxCount()
        {
            try
            {
                int maxCount = MaxCount;
                maxCount = (int)Math.Ceiling((double)maxCount / OptionChains.Count);
                maxCount = (int)Math.Ceiling((double)maxCount / GetStrategiesCount());
                maxCount = (int)Math.Ceiling((double)maxCount / GetTypesCount());
                return maxCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private async Task WriteToFileWhenDone()
        {
            if (SaveWhenDone)
            {
                SaveWhenDone = false;
                switch (ExportFormat.ToUpper())
                {
                    case "DOMINATOR LIST":
                        await WriteSpreadsToFileUsingDominatorFormat(FilePath);
                        break;
                    case "CSV":
                        await Task.Run(() => WriteSpreadsToFile(FilePath));
                        break;
                }
            }
        }

        private int GetStrategiesCount()
        {
            return new List<bool>
                {
                    SingleLegEnabled,
                    VerticalEnabled,
                    OneByTwoRatioEnabled,
                    OneByThreeRatioEnabled,
                    RatioEnabled,
                    CalendarEnabled,
                    DiagonalEnabled,
                    ButterflyEnabled,
                    SkewedButterflyEnabled,
                    TreeEnabled,
                    CalendarButterflyEnabled,
                    IronButterflyEnabled,
                    IronGutFlyEnabled,
                    CondorEnabled,
                    IronCondorEnabled,
                    OneThreeThreeOneEnabled,
                    OneThreeTwoEnabled,
                    TwoThreeOneEnabled,
                    BoxEnabled,
                }
                .Count(x => x);
        }

        private int GetTypesCount()
        {
            return new List<bool>
                {
                    CallsEnabled,
                    PutsEnabled,
                }
                .Count(x => x);
        }

        [Command]
        public async void GenerateSampleSpreads()
        {
            try
            {
                CancellationToken token = new();

                int maxCount = int.MaxValue;

                foreach (OptionChainModel optionChain in OptionChains)
                {
                    List<Option> filteredChain = await FilterOptionChainByStrikeAndExpBoundAsync(optionChain);

                    List<Option> callOptionsChain = filteredChain.Where(x => x.PutCall == PutCall.Call).ToList();
                    List<Option> putOptionsChain = filteredChain.Where(x => x.PutCall == PutCall.Put).ToList();

                    if (SingleLegEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => SingleLegSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                       null,
                                                                                       false,
                                                                                       SingleLegSpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           SingleLegCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => SingleLegSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                       null,
                                                                                       false,
                                                                                       SingleLegSpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           SingleLegPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }
                    }

                    if (VerticalEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => VerticalSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                      callOptionsChain,
                                                                                      VerticalSpreadsSettings,
                                                                                      maxCount,
                                                                                      token,
                                                                                      true), token).ContinueWith(x =>
                                                                                      {
                                                                                          VerticalCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                      }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => VerticalSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                      putOptionsChain,
                                                                                      VerticalSpreadsSettings,
                                                                                      maxCount,
                                                                                      token,
                                                                                      true), token).ContinueWith(x =>
                                                                                      {
                                                                                          VerticalPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                      }, token);
                        }
                    }

                    if (OneByTwoRatioEnabled)
                    {

                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => RatioSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                   callOptionsChain,
                                                                                   OneByTwoRatioSpreadsSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       OneByTwoRatioCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => RatioSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                   putOptionsChain,
                                                                                   OneByTwoRatioSpreadsSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       OneByTwoRatioPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                        }
                    }

                    if (OneByThreeRatioEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => RatioSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                   callOptionsChain,
                                                                                   OneByThreeRatioSpreadsSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       OneByThreeRatioCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => RatioSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                   putOptionsChain,
                                                                                   OneByThreeRatioSpreadsSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       OneByThreeRatioPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                        }
                    }

                    if (RatioEnabled)
                    {
                        RatioSpreadsSettings.SetRatio();
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => RatioSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                   callOptionsChain,
                                                                                   RatioSpreadsSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       RatioCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => RatioSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                   putOptionsChain,
                                                                                   RatioSpreadsSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       RatioPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                        }
                    }

                    if (CalendarEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                      callOptionsChain,
                                                                                      CalendarSpreadsSettings,
                                                                                      maxCount,
                                                                                      token,
                                                                                      true), token).ContinueWith(x =>
                                                                                      {
                                                                                          CalendarCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                      }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => CalendarSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                      putOptionsChain,
                                                                                      CalendarSpreadsSettings,
                                                                                      maxCount,
                                                                                      token,
                                                                                      true), token).ContinueWith(x =>
                                                                                      {
                                                                                          CalendarPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                      }, token);
                        }
                    }

                    if (DiagonalEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                      callOptionsChain,
                                                                                      DiagonalSpreadsSettings,
                                                                                      maxCount,
                                                                                      token,
                                                                                      true), token).ContinueWith(x =>
                                                                                      {
                                                                                          DiagonalCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                      }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => DiagonalSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                      putOptionsChain,
                                                                                      DiagonalSpreadsSettings,
                                                                                      maxCount,
                                                                                      token,
                                                                                      true), token).ContinueWith(x =>
                                                                                      {
                                                                                          DiagonalPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                      }, token);
                        }
                    }

                    if (ButterflyEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => ButterflySpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       ButterflySpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           ButterflyCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => ButterflySpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       ButterflySpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           ButterflyPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }
                    }

                    if (SkewedButterflyEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => SkewedButterflySpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                             callOptionsChain,
                                                                                             callOptionsChain,
                                                                                             SkewedButterflySpreadsSettings,
                                                                                             maxCount,
                                                                                             token,
                                                                                             true), token).ContinueWith(x =>
                                                                                             {
                                                                                                 SkewedButterflyCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                             }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => SkewedButterflySpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                             putOptionsChain,
                                                                                             putOptionsChain,
                                                                                             SkewedButterflySpreadsSettings,
                                                                                             maxCount,
                                                                                             token,
                                                                                             true), token).ContinueWith(x =>
                                                                                             {
                                                                                                 SkewedButterflyPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                             }, token);
                        }
                    }

                    if (TreeEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => TreeSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                             callOptionsChain,
                                                                                             callOptionsChain,
                                                                                             TreeSpreadsSettings,
                                                                                             maxCount,
                                                                                             token,
                                                                                             true), token).ContinueWith(x =>
                                                                                             {
                                                                                                 TreeCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                             }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => TreeSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                             putOptionsChain,
                                                                                             putOptionsChain,
                                                                                             TreeSpreadsSettings,
                                                                                             maxCount,
                                                                                             token,
                                                                                             true), token).ContinueWith(x =>
                                                                                             {
                                                                                                 TreePutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                             }, token);
                        }
                    }

                    if (CalendarButterflyEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => CalendarButterflySpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                             callOptionsChain,
                                                                                             callOptionsChain,
                                                                                             CalendarButterflySpreadsSettings,
                                                                                             maxCount,
                                                                                             token,
                                                                                             true), token).ContinueWith(x =>
                                                                                             {
                                                                                                 CalendarButterflyCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                             }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => CalendarButterflySpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                             putOptionsChain,
                                                                                             putOptionsChain,
                                                                                             CalendarButterflySpreadsSettings,
                                                                                             maxCount,
                                                                                             token,
                                                                                             true), token).ContinueWith(x =>
                                                                                             {
                                                                                                 CalendarButterflyPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                             }, token);
                        }
                    }

                    if (IronButterflyEnabled)
                    {
                        _ = Task.Run(() => IronButterflySpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       IronButterflySpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           IronButterflySpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                    }

                    if (IronGutFlyEnabled)
                    {
                        _ = Task.Run(() => IronGutFlyGenerator.GenerateAsync(callOptionsChain,
                                                                             callOptionsChain,
                                                                             putOptionsChain,
                                                                             putOptionsChain,
                                                                             IronGutFlySpreadsSettings,
                                                                             maxCount,
                                                                             token,
                                                                             true), token).ContinueWith(x =>
                                                                             {
                                                                                 IronGutFlySpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                             }, token);
                    }

                    if (CondorEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => CondorSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                    callOptionsChain,
                                                                                    callOptionsChain,
                                                                                    callOptionsChain,
                                                                                    CondorSpreadsSettings,
                                                                                    maxCount,
                                                                                    token,
                                                                                    true), token).ContinueWith(x =>
                                                                                    {
                                                                                        CondorCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                    }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => CondorSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                    putOptionsChain,
                                                                                    putOptionsChain,
                                                                                    putOptionsChain,
                                                                                    CondorSpreadsSettings,
                                                                                    maxCount,
                                                                                    token,
                                                                                    true), token).ContinueWith(x =>
                                                                                    {
                                                                                        CondorPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                    }, token);
                        }
                    }

                    if (IronCondorEnabled)
                    {
                        _ = Task.Run(() => IronCondorSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                    putOptionsChain,
                                                                                    callOptionsChain,
                                                                                    callOptionsChain,
                                                                                    IronCondorSpreadsSettings,
                                                                                    maxCount,
                                                                                    token,
                                                                                    true), token).ContinueWith(x =>
                                                                                    {
                                                                                        IronCondorSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                    }, token);
                    }

                    if (OneThreeThreeOneEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => OneThreeThreeOneSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                              callOptionsChain,
                                                                                              callOptionsChain,
                                                                                              callOptionsChain,
                                                                                              OneThreeThreeOneSpreadsSettings,
                                                                                              maxCount,
                                                                                              token,
                                                                                              true), token).ContinueWith(x =>
                                                                                              {
                                                                                                  OneThreeThreeOneCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                              }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => OneThreeThreeOneSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                              putOptionsChain,
                                                                                              putOptionsChain,
                                                                                              putOptionsChain,
                                                                                              OneThreeThreeOneSpreadsSettings,
                                                                                              maxCount,
                                                                                              token,
                                                                                              true), token).ContinueWith(x =>
                                                                                              {
                                                                                                  OneThreeThreeOnePutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                              }, token);
                        }
                    }

                    if (OneThreeTwoEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => OneThreeTwoSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       OneThreeTwoSpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           OneThreeTwoCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => OneThreeTwoSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       OneThreeTwoSpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           OneThreeTwoPutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }
                    }

                    if (TwoThreeOneEnabled)
                    {
                        if (CallsEnabled)
                        {
                            _ = Task.Run(() => TwoThreeOneSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       callOptionsChain,
                                                                                       TwoThreeOneSpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           TwoThreeOneCallSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }

                        if (PutsEnabled)
                        {
                            _ = Task.Run(() => TwoThreeOneSpreadsGenerator.GenerateAsync(putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       putOptionsChain,
                                                                                       TwoThreeOneSpreadsSettings,
                                                                                       maxCount,
                                                                                       token,
                                                                                       true), token).ContinueWith(x =>
                                                                                       {
                                                                                           TwoThreeOnePutSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                       }, token);
                        }
                    }

                    if (BoxEnabled)
                    {
                        _ = Task.Run(() => BoxSpreadsGenerator.GenerateAsync(callOptionsChain,
                                                                                  callOptionsChain,
                                                                                  putOptionsChain,
                                                                                  putOptionsChain,
                                                                                   BoxSpreadsGeneratorSettings,
                                                                                   maxCount,
                                                                                   token,
                                                                                   true), token).ContinueWith(x =>
                                                                                   {
                                                                                       BoxSpreadSample = x.Result.Spreads.FirstOrDefault()?.Symbol;
                                                                                   }, token);
                    }
                }

            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateSampleSpreads));
            }
        }

        private string[] ProcessResults(List<SpreadGeneratorResults> spreadsResults)
        {
            int count = spreadsResults.Sum(x => x.Spreads.Count);
            string[] spreads = new string[count];
            int index = 0;
            foreach (SpreadGeneratorResults spreadsResult in spreadsResults)
            {
                foreach (Spread spread in spreadsResult.Spreads)
                {
                    spreads[index++] = spread.Symbol;
                }
            }

            return spreads;
        }

        [Command]
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        private async void WriteSpreadsToFile(string filePath)
        {
            SaveWhenDone = false;

            if (CrateSeparateExportForUnderlyings)
            {
                string fullPath = Path.GetFullPath(Path.GetDirectoryName(filePath) ?? string.Empty);
                string fileName = Path.GetFileName(filePath);

                IEnumerable<IGrouping<string, SpreadGeneratorResults>> grouped = LatestSpreadGeneratorResults.GroupBy(x => x.Underlying);
                foreach (IGrouping<string, SpreadGeneratorResults> group in grouped)
                {
                    List<SpreadGeneratorResults> list = group.ToList();
                    string targetFile = group.Key + fileName;
                    string path = Path.Combine(fullPath, targetFile);
                    await SaveToFile(path, list);
                }
            }
            else
            {
                List<SpreadGeneratorResults> list = LatestSpreadGeneratorResults.ToList();
                await SaveToFile(filePath, list);
            }
        }

        private async Task SaveToFile(string filePath, List<SpreadGeneratorResults> list)
        {
            try
            {
                List<string> spreads = (await Task.Run(() => ProcessResults(list))).ToList();
                if (RandomizeExport)
                {
                    ListHelper.Shuffle(spreads);
                }
                FileStream file = new(filePath, FileMode.Create);
                StreamWriter streamWriter = new(file, Encoding.Default);

                foreach (string spread in spreads)
                {
                    streamWriter.WriteLine(spread);
                }
                streamWriter.Close();
                file.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveToFile));
            }
        }

        private async Task WriteSpreadsToFileUsingDominatorFormat(string filePath)
        {
            try
            {
                SaveWhenDone = false;

                using Workbook workbook = new();
                Worksheet worksheet = workbook.Worksheets[0];
                workbook.BeginUpdate();
                try
                {
                    int rowsCount = Math.Max(2, LatestSpreadGeneratorResults.Sum(x => x.Spreads.Count));
                    object[,] values = new object[rowsCount, 24];

                    values[0, 21] = DateTime.Today.ToString("M.d.yy");
                    values[0, 22] = LatestSpreadGeneratorResults.FirstOrDefault()?.Strategy.ToString();
                    values[1, 21] = OmsCore.User.Username;
                    Dictionary<string, double> underlyingSymbolToLastPriceMap = new();
                    int index = -1;
                    for (int i = 0; i < LatestSpreadGeneratorResults.Count; i++)
                    {
                        SpreadGeneratorResults spreadResult = LatestSpreadGeneratorResults[i];
                        List<Spread> spreads = spreadResult.Spreads.Where(x => x != null).ToList();
                        foreach (Spread spread in spreads)
                        {
                            index++;

                            int legsCount = spread.Legs.Count;
                            SpreadLeg leg1 = legsCount > 0 ? spread.Legs[0] : null;
                            SpreadLeg leg2 = legsCount > 1 ? spread.Legs[1] : null;
                            SpreadLeg leg3 = legsCount > 2 ? spread.Legs[2] : null;
                            SpreadLeg leg4 = legsCount > 3 ? spread.Legs[3] : null;
                            string underlying = leg1.Option?.Underlying.Symbol;
                            if (!underlyingSymbolToLastPriceMap.ContainsKey(underlying))
                            {
                                double lastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(underlying, SubscriptionFieldType.LastPrice);
                                underlyingSymbolToLastPriceMap[underlying] = lastPrice;
                            }

                            if (TimeHelper.IsThirdFridayOfTheMonth(leg1.Option.Expiration) &&
                                leg1.Option?.Underlying.Symbol?.Replace("$", "") == leg1.Option?.RootSymbol)
                            {
                                values[index, 0] = leg1.Option?.Expiration.ToString("MMM yy").ToUpper();
                            }
                            else
                            {
                                values[index, 0] = leg1.Option?.Expiration.ToString("dd_MMM_yy").ToUpper();
                            }

                            switch (spreadResult.Strategy)
                            {
                                case Strategy.Vertical:
                                case Strategy.Ratio1X2:
                                case Strategy.Ratio1X3:
                                case Strategy.RatioCustom:
                                case Strategy.Butterfly:
                                case Strategy.SkewedButterfly:
                                case Strategy.Tree:
                                case Strategy.CalendarButterfly:
                                case Strategy.IronButterfly:
                                case Strategy.Condor:
                                case Strategy.IronCondor:
                                case Strategy.OneThreeThreeOne:
                                    values[index, 1] = leg1.Option?.Strike;
                                    values[index, 2] = leg2.Option?.Strike;
                                    if (legsCount > 2)
                                    {
                                        values[index, 12] = leg3.Option?.Strike;
                                    }
                                    break;
                                case Strategy.Calendar:
                                case Strategy.Diagonal:
                                    if (TimeHelper.IsThirdFridayOfTheMonth(leg2.Option.Expiration) &&
                                        leg2.Option?.Underlying.Symbol?.Replace("$", "") == leg2.Option?.RootSymbol)
                                    {
                                        values[index, 1] = leg2.Option?.Expiration.ToString("MMM yy").ToUpper();
                                    }
                                    else
                                    {
                                        values[index, 1] = leg2.Option?.Expiration.ToString("dd_MMM_yy").ToUpper();
                                    }

                                    values[index, 2] = leg1.Option?.Strike;
                                    values[index, 12] = leg2.Option?.Strike;
                                    break;
                            }

                            switch (spreadResult.Strategy)
                            {
                                case Strategy.Vertical:
                                    values[index, 4] = "1X1";
                                    break;
                                case Strategy.Ratio1X2:
                                    values[index, 4] = "1X2";
                                    break;
                                case Strategy.Ratio1X3:
                                    values[index, 4] = "1X3";
                                    break;
                                case Strategy.Diagonal:
                                    if (spread.Ratios != null &&
                                        spread.Ratios.Length == 2 &&
                                        spread.Ratios[0] != spread.Ratios[1])
                                    {
                                        values[index, 4] = spread.Ratios[0] + "X" + spread.Ratios[1];
                                    }
                                    break;
                                case Strategy.Butterfly:
                                case Strategy.SkewedButterfly:
                                case Strategy.CalendarButterfly:
                                case Strategy.IronButterfly:
                                    values[index, 4] = "FLY";
                                    break;
                                case Strategy.Tree:
                                    values[index, 4] = "TREE";
                                    break;
                            }


                            values[index, 3] = spreadResult.Type == PutCall.Call ? "Call" : "Put";
                            values[index, 7] = spread.Symbol;
                            values[index, 14] = leg1?.Option?.Symbol;
                            values[index, 15] = leg2?.Option?.Symbol;
                            values[index, 16] = leg3?.Option?.Symbol;
                            values[index, 17] = leg4?.Option?.Symbol;
                            values[index, 18] = spreadResult.Underlying.Replace("$", "");
                            values[index, 23] = 1;
                        }
                    }

                    values[0, 20] = string.Join(";", underlyingSymbolToLastPriceMap.Keys.Select(x => x.Replace("$", "")));
                    values[1, 22] = string.Join(";", underlyingSymbolToLastPriceMap.Select(x => x.Key + "," + x.Value));

                    if (RandomizeExport)
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
                                object temp = values[row, col];
                                values[row, col] = values[swapRow, col];
                                values[swapRow, col] = temp;
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
                _ = Dispatcher?.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private string GetTitle(bool withSymbols = true)
        {
            string symbols = "";
            if (withSymbols)
            {
                symbols = string.Join(", ", OptionChains.Select(x => x.Symbol));
            }
            var types = CallsEnabled ? "- C" : "";
            types += PutsEnabled ? "- P" : "";
            List<string> strategies = new();
            if (SingleLegEnabled)
            {
                strategies.Add("Single Leg");
            }
            if (VerticalEnabled)
            {
                strategies.Add("Vertical");
            }
            if (OneByTwoRatioEnabled)
            {
                strategies.Add("1X2 Ratio");
            }
            if (OneByThreeRatioEnabled)
            {
                strategies.Add("1X3 Ratio");
            }
            if (RatioEnabled)
            {
                strategies.Add($"{RatioSpreadsSettings.Leg1Ratio}X{RatioSpreadsSettings.Leg2Ratio} Ratio");
            }
            if (CalendarEnabled)
            {
                strategies.Add("Calendar");
            }
            if (DiagonalEnabled)
            {
                strategies.Add("Diagonal");
            }
            if (ButterflyEnabled)
            {
                strategies.Add("Butterfly");
            }
            if (SkewedButterflyEnabled)
            {
                strategies.Add("Skewed Butterfly");
            }
            if (TreeEnabled)
            {
                strategies.Add("Tree");
            }
            if (CalendarButterflyEnabled)
            {
                strategies.Add("Calendar Butterfly");
            }
            if (IronButterflyEnabled)
            {
                strategies.Add("Iron Butterfly");
            }
            if (IronGutFlyEnabled)
            {
                strategies.Add("Iron Gut Fly");
            }
            if (CondorEnabled)
            {
                strategies.Add("Condor");
            }
            if (IronCondorEnabled)
            {
                strategies.Add("Iron Condor");
            }
            if (OneThreeThreeOneEnabled)
            {
                strategies.Add("1X3X3X1");
            }
            if (OneThreeTwoEnabled)
            {
                strategies.Add("1X3X2");
            }
            if (TwoThreeOneEnabled)
            {
                strategies.Add("2X3X1");
            }
            if (BoxEnabled)
            {
                strategies.Add("Box");
            }

            string selectedStrategies = string.Join(", ", strategies);

            return $"{symbols} {types.Trim()} - {selectedStrategies} Spreads";
        }

        public static async Task<(List<Option> callOptionsChain, List<Option> putOptionsChain)> ApplySpreadGeneratorFilters(SpreadsGeneratorConfig config, List<Option> filteredChain, PortfolioManagerModel portfolioManager, CancellationToken token)
        {
            if (config.MinStrikeEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Strike >= config.MinStrike).ToList();
            }

            if (config.MaxStrikeEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Strike <= config.MaxStrike).ToList();
            }

            if (config.WholeStrikes && !config.DecimalStrikes)
            {
                filteredChain = filteredChain.Where(x => x.Strike.IsWhole()).ToList();
            }

            if (!config.WholeStrikes && config.DecimalStrikes)
            {
                filteredChain = filteredChain.Where(x => !x.Strike.IsWhole()).ToList();
            }

            if (config.MinStrikeOccurrenceEnabled)
            {
                List<Option> selected = new();
                foreach (IGrouping<double, Option> group in filteredChain.GroupBy(x => x.Strike))
                {
                    if (group.Select(x => x.Expiration).Distinct().Count() >= config.MinStrikeOccurrence)
                    {
                        selected.AddRange(group.ToList());
                    }
                }
                filteredChain = selected;
            }

            if (config.MaxStrikeOccurrenceEnabled)
            {
                List<Option> selected = new();
                foreach (IGrouping<double, Option> group in filteredChain.GroupBy(x => x.Strike))
                {
                    if (group.Select(x => x.Expiration).Distinct().Count() <= config.MaxStrikeOccurrence)
                    {
                        selected.AddRange(group.ToList());
                    }
                }
                filteredChain = selected;
            }

            string underlying = filteredChain.FirstOrDefault()?.Underlying?.Symbol;
            if (config.StrikeDistanceFromLastPercentEnabled)
            {
                if (filteredChain.Count > 0)
                {
                    var _lastStore = new DataStore();
                    if (underlying != null)
                    {
                        _lastStore.GetQuoteDataFor(underlying, SubscriptionFieldType.LastPrice);
                        double lastPx = await _lastStore.GetDataAsync(underlying);

                        double percentage = config.StrikeDistanceFromLastPercent * lastPx;
                        filteredChain = filteredChain.Where(x => Math.Abs(x.Strike - lastPx) <= percentage).ToList();
                    }
                }
            }

            if (config.DteRangeEnabled)
            {
                filteredChain = filteredChain.Where(x => (x.Expiration.Date - DateTime.Today).TotalDays >= config.MinDteRange &&
                                                         (x.Expiration.Date - DateTime.Today).TotalDays <= config.MaxDteRange).ToList();
            }

            List<DateTime> expirationsToSkip = config.ExcludedExpirations.Select(x => x.Date.Date).ToList();
            filteredChain = filteredChain.Where(x => !expirationsToSkip.Contains(x.Expiration.Date)).ToList();

            filteredChain = filteredChain.Where(x => x.Expiration >= DateTime.Now).ToList();

            if (config.MinExpEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Expiration >= config.MinExp).ToList();
            }

            if (config.MaxExpEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Expiration <= config.MaxExp).ToList();
            }

            if (config.Regulars && !config.NonRegulars)
            {
                if (underlying == "$VIX")
                {
                    filteredChain = filteredChain.Where(x => (TimeHelper.IsThirdWednesdayOfTheMonth(x.Expiration) && underlying.Contains(x.RootSymbol)) || underlying.Contains(x.RootSymbol)).ToList();
                }
                else
                {
                    filteredChain = filteredChain.Where(x => (TimeHelper.IsThirdFridayOfTheMonth(x.Expiration) && underlying.Contains(x.RootSymbol)) || underlying.Contains(x.RootSymbol)).ToList();
                }
            }

            if (!config.Regulars && config.NonRegulars)
            {
                filteredChain = filteredChain.Where(x => !underlying.Contains(x.RootSymbol)).ToList();
            }

            if (!config.Regulars && !config.NonRegulars && config.Quarterlies)
            {
                filteredChain = filteredChain.Where(x => TimeHelper.IsLastDayOfTheMonth(x.Expiration)).ToList();
            }

            if (!config.Quarterlies)
            {
                filteredChain = filteredChain.Where(x => !TimeHelper.IsLastDayOfTheMonth(x.Expiration)).ToList();
            }

            if (config.Leg1LockEnabled)
            {
                HashSet<string> leg1FilterOption =
                    (config.Leg1LockOptions ?? "").Split(",").Select(x => x.Trim()).ToHashSet();
                filteredChain = filteredChain.Where(x => leg1FilterOption.Contains(x.Symbol)).ToList();
            }


            if (config.Leg1OpenInterestFilter is { Enabled: true })
            {
                if (config.Leg1OpenInterestFilter.Enabled)
                {
                    filteredChain = new List<Option>();
                }

                DataStore openInterestStore = new(CancellationToken.None, OmsCore.Config.SpreadGeneratorTimeout,
                    OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                openInterestStore.GetQuoteDataFor(filteredChain, SubscriptionFieldType.OpenInterest);

                foreach (Option option in filteredChain)
                {
                    double openInterest = await openInterestStore.GetDataAsync(option.Symbol);

                    if (config.Leg1OpenInterestFilter.Enabled && SpreadsGeneratorViewModel.ApplyFilter(config.Leg1OpenInterestFilter.Filter,
                            openInterest, config.Leg1OpenInterestFilter.Value))
                    {
                        filteredChain.Add(option);
                    }
                }
            }


            if (config.Leg1VolumeFilter is { Enabled: true })
            {
                DataStore volumeStore = new(token, OmsCore.Config.SpreadGeneratorTimeout,
                    OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                volumeStore.GetQuoteDataFor(filteredChain, SubscriptionFieldType.Volume);

                if (config.Leg1VolumeFilter.Enabled)
                {
                    List<Option> selected = new();
                    foreach (var leg1 in filteredChain)
                    {
                        token.ThrowIfCancellationRequested();
                        double volume = await volumeStore.GetDataAsync(leg1.Symbol);
                        if (SpreadsGeneratorViewModel.ApplyFilter(config.Leg1VolumeFilter.Filter, volume, config.Leg1VolumeFilter.Value))
                        {
                            selected.Add(leg1);
                        }
                    }

                    filteredChain = selected;
                }
            }

            List<Option> leg1CallOptionsChain = filteredChain.Where(x => x.PutCall == PutCall.Call).ToList();
            List<Option> leg1PutOptionsChain = filteredChain.Where(x => x.PutCall == PutCall.Put).ToList();

            List<Option> callOptionsChain = leg1CallOptionsChain;
            List<Option> putOptionsChain = leg1PutOptionsChain;

            if (config.SingleLegEnabled)
            {
                if (config.StrikeIncludeFromTopAndBottomEnabled &&
                    config.StrikeIncludeFromTopAndBottomCount > 0)
                {
                    List<Option> callOptionsToInclude = new();
                    List<Option> putOptionsToInclude = new();

                    var callsGrouped = callOptionsChain.GroupBy(x => x.Expiration);
                    foreach (var expGroup in callsGrouped)
                    {
                        var top = expGroup.OrderBy(x => x.Strike)
                            .Take(config.StrikeIncludeFromTopAndBottomCount);
                        var bottom = expGroup.OrderByDescending(x => x.Strike)
                            .Take(config.StrikeIncludeFromTopAndBottomCount);
                        callOptionsToInclude.AddRange(top);
                        callOptionsToInclude.AddRange(bottom);
                    }

                    var putsGrouped = putOptionsChain.GroupBy(x => x.Expiration);
                    foreach (var expGroup in putsGrouped)
                    {
                        var top = expGroup.OrderBy(x => x.Strike)
                            .Take(config.StrikeIncludeFromTopAndBottomCount);
                        var bottom = expGroup.OrderByDescending(x => x.Strike)
                            .Take(config.StrikeIncludeFromTopAndBottomCount);
                        putOptionsToInclude.AddRange(top);
                        putOptionsToInclude.AddRange(bottom);
                    }
                }

                if (config.SingleLegSpreadsSettings != null && config.SingleLegSpreadsSettings.ExcludedTradedSymbols)
                {
                    callOptionsChain = portfolioManager.GetNonTradedByFirm(leg1CallOptionsChain);
                    putOptionsChain = portfolioManager.GetNonTradedByFirm(leg1PutOptionsChain);
                }
            }

            return (callOptionsChain, putOptionsChain);
        }

        private async Task<List<Option>> FilterOptionChainByStrikeAndExpBoundAsync(OptionChainModel optionChain)
        {
            List<Option> filteredChain = optionChain.OptionChain.ToList();

            if (MinStrikeEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Strike >= MinStrike).ToList();
            }

            if (MaxStrikeEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Strike <= MaxStrike).ToList();
            }

            if (WholeStrikes && !DecimalStrikes)
            {
                filteredChain = filteredChain.Where(x => x.Strike.IsWhole()).ToList();
            }

            if (!WholeStrikes && DecimalStrikes)
            {
                filteredChain = filteredChain.Where(x => !x.Strike.IsWhole()).ToList();
            }

            if (MinStrikeOccurrenceEnabled)
            {
                List<Option> selected = new();
                foreach (IGrouping<double, Option> group in filteredChain.GroupBy(x => x.Strike))
                {
                    if (group.Select(x => x.Expiration).Distinct().Count() >= MinStrikeOccurrence)
                    {
                        selected.AddRange(group.ToList());
                    }
                }
                filteredChain = selected;
            }

            if (MaxStrikeOccurrenceEnabled)
            {
                List<Option> selected = new();
                foreach (IGrouping<double, Option> group in filteredChain.GroupBy(x => x.Strike))
                {
                    if (group.Select(x => x.Expiration).Distinct().Count() <= MaxStrikeOccurrence)
                    {
                        selected.AddRange(group.ToList());
                    }
                }
                filteredChain = selected;
            }

            if (StrikeDistanceFromLastPercentEnabled)
            {
                if (filteredChain.Count > 0)
                {
                    _lastStore.GetQuoteDataFor(optionChain.Symbol, SubscriptionFieldType.LastPrice);
                    double lastPx = await _lastStore.GetDataAsync(optionChain.Symbol);

                    double percentage = StrikeDistanceFromLastPercent * lastPx;
                    filteredChain = filteredChain.Where(x => Math.Abs(x.Strike - lastPx) <= percentage).ToList();
                }
            }

            if (DteRangeEnabled)
            {
                filteredChain = filteredChain.Where(x => (x.Expiration.Date - DateTime.Today).TotalDays >= MinDteRange &&
                                                         (x.Expiration.Date - DateTime.Today).TotalDays <= MaxDteRange).ToList();
            }

            List<DateTime> expirationsToSkip = ExpirationsList.Where(x => !x.IsChecked).Select(x => x.Date.Date).ToList();
            filteredChain = filteredChain.Where(x => !expirationsToSkip.Contains(x.Expiration.Date)).ToList();

            filteredChain = filteredChain.Where(x => x.Expiration >= DateTime.Now).ToList();

            if (MinExpEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Expiration >= MinExp).ToList();
            }

            if (MaxExpEnabled)
            {
                filteredChain = filteredChain.Where(x => x.Expiration <= MaxExp).ToList();
            }

            if (Regulars && !NonRegulars)
            {
                if (optionChain.Symbol == "$VIX")
                {
                    filteredChain = filteredChain.Where(x => (TimeHelper.IsThirdWednesdayOfTheMonth(x.Expiration) && optionChain.Symbol.Contains(x.RootSymbol)) || optionChain.Symbol.Contains(x.RootSymbol)).ToList();
                }
                else
                {
                    filteredChain = filteredChain.Where(x => (TimeHelper.IsThirdFridayOfTheMonth(x.Expiration) && optionChain.Symbol.Contains(x.RootSymbol)) || optionChain.Symbol.Contains(x.RootSymbol)).ToList();
                }
            }

            if (!Regulars && NonRegulars)
            {
                filteredChain = filteredChain.Where(x => !optionChain.Symbol.Contains(x.RootSymbol)).ToList();
            }

            if (!Regulars && !NonRegulars && Quarterlies)
            {
                filteredChain = filteredChain.Where(x => TimeHelper.IsLastDayOfTheMonth(x.Expiration)).ToList();
            }

            if (!Quarterlies)
            {
                filteredChain = filteredChain.Where(x => !TimeHelper.IsLastDayOfTheMonth(x.Expiration)).ToList();
            }

            return filteredChain;
        }

        private async Task AddMultipleOptionChainsAsync(List<OptionChainModel> optionChains)
        {
            IOrderedEnumerable<DateTime> expirations = optionChains.SelectMany(x => x.OptionChain)
                                          .Select(x => x.Expiration.Date)
                                          .Distinct()
                                          .OrderBy(x => x);
            Options = optionChains.SelectMany(x => x.OptionChain)
                                  .Select(x => x.Symbol)
                                  .ToHashSet();

            var beginInvoke = DispatcherService.BeginInvoke(() =>
            {
                foreach (OptionChainModel optionChain in optionChains)
                {
                    if (OptionChains.All(x => x.Symbol != optionChain.Symbol))
                    {
                        OptionChains.Add(optionChain);
                    }
                }
                ExpirationsList.Clear();
                CalendarTargetExpirationsList.Clear();
                DiagonalTargetExpirationsList.Clear();

                foreach (DateTime exp in expirations)
                {
                    ExpirationsList.Add(new ExpirationModel(exp, MinExpirationChangedCommand, MaxExpirationChangedCommand));
                    CalendarTargetExpirationsList.Add(new ExpirationModel(exp));
                    DiagonalTargetExpirationsList.Add(new ExpirationModel(exp));
                }
            });
            beginInvoke.ContinueWith(_ => CheckIfGeneratingIsAllowed());
            beginInvoke.ContinueWith(x => GenerateSampleSpreads());
            beginInvoke.ContinueWith(x => CalculateStatsAndSamples());
            await beginInvoke;
        }

        private void CheckIfGeneratingIsAllowed()
        {
            AllowGenerating = OptionChains.Count > 0 || ConfigChains.Any(x => x.Config != null);
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return;
                }
                SpreadsGeneratorConfig config = await Task.Run(() => JsonConvert.DeserializeObject<SpreadsGeneratorConfig>(configJson, SpreadGeneratorConfigSerializationSettings));
                await LoadFromConfigAsync(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeserializeAndLoadConfig));
            }
        }

        private async Task LoadFromConfigAsync(SpreadsGeneratorConfig config)
        {
            OptionChains.Clear();

            CallsEnabled = config.CallsEnabled;
            PutsEnabled = config.PutsEnabled;
            WholeStrikes = config.WholeStrikes;
            DecimalStrikes = config.DecimalStrikes;
            MinStrikeEnabled = config.MinStrikeEnabled;
            MaxStrikeEnabled = config.MaxStrikeEnabled;
            MinStrike = config.MinStrike;
            MaxStrike = config.MaxStrike;
            MinStrikeOccurrenceEnabled = config.MinStrikeOccurrenceEnabled;
            MaxStrikeOccurrenceEnabled = config.MaxStrikeOccurrenceEnabled;
            MinStrikeOccurrence = config.MinStrikeOccurrence;
            MaxStrikeOccurrence = config.MaxStrikeOccurrence;
            StrikeDistanceFromLastPercentEnabled = config.StrikeDistanceFromLastPercentEnabled;
            StrikeDistanceFromLastPercent = config.StrikeDistanceFromLastPercent;
            StrikeIncludeAsAdditionFromTopAndBottom = config.StrikeIncludeAsAdditionFromTopAndBottom;
            StrikeIncludeFromTopAndBottomEnabled = config.StrikeIncludeFromTopAndBottomEnabled;
            StrikeIncludeFromTopAndBottomCount = config.StrikeIncludeFromTopAndBottomCount;
            MinExpEnabled = config.MinExpEnabled;
            MaxExpEnabled = config.MaxExpEnabled;
            MinExp = config.MinExp;
            MaxExp = config.MaxExp;
            Regulars = config.Regulars;
            Quarterlies = config.Quarterlies;
            FreshSpreads = config.FreshSpreads;
            AttemptedSpreads = config.AttemptedSpreads;
            DteRangeEnabled = config.DteRangeEnabled;
            ApplyDteToExpSpacingMap = config.ApplyDteToExpSpacingMap;
            MinDteRange = config.MinDteRange;
            MaxDteRange = config.MaxDteRange;
            NonRegulars = config.NonRegulars;
            TopPercentageSelectionEnabled = config.TopPercentageSelectionEnabled;
            TopPercentageSelectionCount = config.TopPercentageSelectionCount;
            TopPercentageSelectionSortingMode = config.TopPercentageSelectionSortingMode;
            MaxCountEnabled = config.MaxCountEnabled;
            Leg1LockEnabled = config.Leg1LockEnabled;
            Leg2LockEnabled = config.Leg2LockEnabled;
            Leg3LockEnabled = config.Leg3LockEnabled;
            Leg4LockEnabled = config.Leg4LockEnabled;
            Leg1LockOptions = config.Leg1LockOptions;
            Leg2LockOptions = config.Leg2LockOptions;
            Leg3LockOptions = config.Leg3LockOptions;
            Leg4LockOptions = config.Leg4LockOptions;
            ParsedOutputCount = config.ParsedOutputCount;
            ParsedBuildCount = config.ParsedBuildCount;
            ParsedOutputEnabled = config.ParsedOutputEnabled;
            ApproximateMissingQuotes = config.ApproximateMissingQuotes;
            ApproximateMissingGreeks = config.ApproximateMissingGreeks;
            ApproximateMissingHanweck = config.ApproximateMissingHanweck;
            MaxCount = config.MaxCount;

            SingleLegEnabled = config.SingleLegEnabled;
            VerticalEnabled = config.VerticalEnabled;
            OneByTwoRatioEnabled = config.OneByTwoRatioEnabled;
            OneByThreeRatioEnabled = config.OneByThreeRatioEnabled;
            RatioEnabled = config.RatioEnabled;
            ButterflyEnabled = config.ButterflyEnabled;
            SkewedButterflyEnabled = config.SkewedButterflyEnabled;
            TreeEnabled = config.TreeEnabled;
            CalendarButterflyEnabled = config.CalendarButterflyEnabled;
            IronButterflyEnabled = config.IronButterflyEnabled;
            IronGutFlyEnabled = config.IronGutFlyEnabled;
            CalendarEnabled = config.CalendarEnabled;
            DiagonalEnabled = config.DiagonalEnabled;
            CondorEnabled = config.CondorEnabled;
            IronCondorEnabled = config.IronCondorEnabled;
            OneThreeThreeOneEnabled = config.OneThreeThreeOneEnabled;
            OneThreeTwoEnabled = config.OneThreeTwoEnabled;
            TwoThreeOneEnabled = config.TwoThreeOneEnabled;
            BoxEnabled = config.BoxEnabled;
            ExportToFile = config.ExportToFile;
            OpenInBasket = config.OpenInBasket;
            UnderlyingQuery = config.UnderlyingQuery;
            RunnerOption = config.RunnerOption == RunnerOption.Server || !config.ParsedOutputEnabled ? RunnerOption.Server : RunnerOption.Local;

            if (config.Leg1OpenInterestFilter != null)
            {
                Leg1OpenInterestFilter = config.Leg1OpenInterestFilter;
            }

            if (config.Leg2OpenInterestFilter != null)
            {
                Leg2OpenInterestFilter = config.Leg2OpenInterestFilter;
            }

            if (config.Leg3OpenInterestFilter != null)
            {
                Leg3OpenInterestFilter = config.Leg3OpenInterestFilter;
            }

            if (config.Leg4OpenInterestFilter != null)
            {
                Leg4OpenInterestFilter = config.Leg4OpenInterestFilter;
            }

            if (config.Leg1VolumeFilter != null)
            {
                Leg1VolumeFilter = config.Leg1VolumeFilter;
            }

            if (config.Leg2VolumeFilter != null)
            {
                Leg2VolumeFilter = config.Leg2VolumeFilter;
            }

            if (config.Leg3VolumeFilter != null)
            {
                Leg3VolumeFilter = config.Leg3VolumeFilter;
            }

            if (config.Leg4VolumeFilter != null)
            {
                Leg4VolumeFilter = config.Leg4VolumeFilter;
            }

            if (config.SingleLegSpreadsSettings != null)
            {
                SingleLegSpreadsSettings = config.SingleLegSpreadsSettings as SingleLegSpreadsGeneratorSettingsModel;
            }

            if (config.VerticalSpreadsSettings != null)
            {
                VerticalSpreadsSettings = config.VerticalSpreadsSettings as VerticalSpreadsGeneratorSettingsModel;
            }

            if (config.OneByTwoRatioSpreadsSettings != null)
            {
                OneByTwoRatioSpreadsSettings = config.OneByTwoRatioSpreadsSettings as RatioSpreadsGeneratorSettingsModel;
            }

            if (config.OneByThreeRatioSpreadsSettings != null)
            {
                OneByThreeRatioSpreadsSettings = config.OneByThreeRatioSpreadsSettings as RatioSpreadsGeneratorSettingsModel;
            }

            if (config.RatioSpreadsSettings != null)
            {
                RatioSpreadsSettings = config.RatioSpreadsSettings as RatioSpreadsGeneratorSettingsModel;
            }

            if (config.CalendarSpreadsSettings != null)
            {
                CalendarSpreadsSettings = config.CalendarSpreadsSettings as CalendarSpreadsGeneratorSettingsModel;
            }

            if (config.DiagonalSpreadsSettings != null)
            {
                DiagonalSpreadsSettings = config.DiagonalSpreadsSettings as DiagonalSpreadsGeneratorSettingsModel;
            }

            if (config.ButterflySpreadsSettings != null)
            {
                ButterflySpreadsSettings = config.ButterflySpreadsSettings as ButterflySpreadsGeneratorSettingsModel;
            }

            if (config.SkewedButterflySpreadsSettings != null)
            {
                SkewedButterflySpreadsSettings = config.SkewedButterflySpreadsSettings as SkewedButterflySpreadsGeneratorSettingsModel;
            }

            if (config.TreeSpreadsSettings != null)
            {
                TreeSpreadsSettings = config.TreeSpreadsSettings as TreeSpreadsGeneratorSettingsModel;
            }

            if (config.CalendarButterflySpreadsSettings != null)
            {
                CalendarButterflySpreadsSettings = config.CalendarButterflySpreadsSettings as CalendarButterflySpreadsGeneratorSettingsModel;
            }

            if (config.IronButterflySpreadsSettings != null)
            {
                IronButterflySpreadsSettings = config.IronButterflySpreadsSettings as IronFlySpreadsGeneratorSettingsModel;
            }

            if (config.IronGutFlySpreadsSettings != null)
            {
                IronGutFlySpreadsSettings = config.IronGutFlySpreadsSettings as IronFlySpreadsGeneratorSettingsModel;
            }

            if (config.CondorSpreadsSettings != null)
            {
                CondorSpreadsSettings = config.CondorSpreadsSettings as CondorSpreadsGeneratorSettingsModel;
            }

            if (config.IronCondorSpreadsSettings != null)
            {
                IronCondorSpreadsSettings = config.IronCondorSpreadsSettings as IronCondorSpreadsGeneratorSettingsModel;
            }

            if (config.OneThreeThreeOneSpreadsSettings != null)
            {
                OneThreeThreeOneSpreadsSettings = config.OneThreeThreeOneSpreadsSettings as OneThreeThreeOneSpreadsSettingsModel;
            }

            if (config.OneThreeTwoSpreadsSettings != null)
            {
                OneThreeTwoSpreadsSettings = config.OneThreeTwoSpreadsSettings as OneThreeTwoSpreadsGeneratorSettingsModel;
            }

            if (config.TwoThreeOneSpreadsSettings != null)
            {
                TwoThreeOneSpreadsSettings = config.TwoThreeOneSpreadsSettings as OneThreeTwoSpreadsGeneratorSettingsModel;
                TwoThreeOneSpreadsSettings.Reversed = true;
            }

            if (config.BoxSpreadsSettings != null)
            {
                BoxSpreadsGeneratorSettings = config.BoxSpreadsSettings as BoxSpreadsGeneratorSettingsModel;
            }

            await SearchUnderlying();

            if (config.ExcludedExpirations != null)
            {
                foreach (DateTime exp in config.ExcludedExpirations)
                {
                    ExpirationModel expirationModel = ExpirationsList.FirstOrDefault(x => x.Date == exp.Date);
                    if (expirationModel != null)
                    {
                        expirationModel.IsChecked = false;
                    }
                }
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return JsonConvert.SerializeObject(GetConfig(withContent), Newtonsoft.Json.Formatting.Indented, SpreadGeneratorConfigSerializationSettings);
        }

        public async Task<string> GetSpreadsJsonAsync()
        {
            List<string> spreads = (await Task.Run(() => ProcessResults(LatestSpreadGeneratorResults.ToList()))).ToList();
            if (RandomizeExport)
            {
                ListHelper.Shuffle(spreads);
            }
            return JsonConvert.SerializeObject(spreads, Newtonsoft.Json.Formatting.Indented);
        }

        public SpreadsGeneratorConfig GetConfig(bool withContent = true, bool activeOnly = false)
        {
            List<string> underlyings = withContent ? GetUnderlyings(activeOnly) : [];

            return new SpreadsGeneratorConfig
            {
                ExcludedExpirations = ExpirationsList.Where(x => !x.IsChecked).Select(x => x.Date).ToList(),
                CallsEnabled = CallsEnabled,
                PutsEnabled = PutsEnabled,
                WholeStrikes = WholeStrikes,
                DecimalStrikes = DecimalStrikes,
                MinStrikeEnabled = MinStrikeEnabled,
                MaxStrikeEnabled = MaxStrikeEnabled,
                MinStrike = MinStrike,
                MaxStrike = MaxStrike,
                MinStrikeOccurrenceEnabled = MinStrikeOccurrenceEnabled,
                MaxStrikeOccurrenceEnabled = MaxStrikeOccurrenceEnabled,
                MinStrikeOccurrence = MinStrikeOccurrence,
                MaxStrikeOccurrence = MaxStrikeOccurrence,
                StrikeDistanceFromLastPercentEnabled = StrikeDistanceFromLastPercentEnabled,
                StrikeDistanceFromLastPercent = StrikeDistanceFromLastPercent,
                StrikeIncludeAsAdditionFromTopAndBottom = StrikeIncludeAsAdditionFromTopAndBottom,
                StrikeIncludeFromTopAndBottomEnabled = StrikeIncludeFromTopAndBottomEnabled,
                StrikeIncludeFromTopAndBottomCount = StrikeIncludeFromTopAndBottomCount,
                MinExpEnabled = MinExpEnabled,
                MaxExpEnabled = MaxExpEnabled,
                MinExp = MinExp,
                MaxExp = MaxExp,
                Regulars = Regulars,
                NonRegulars = NonRegulars,
                Quarterlies = Quarterlies,
                FreshSpreads = FreshSpreads,
                AttemptedSpreads = AttemptedSpreads,
                DteRangeEnabled = DteRangeEnabled,
                ApplyDteToExpSpacingMap = ApplyDteToExpSpacingMap,
                MinDteRange = MinDteRange,
                MaxDteRange = MaxDteRange,
                Leg1LockEnabled = Leg1LockEnabled,
                Leg2LockEnabled = Leg2LockEnabled,
                Leg3LockEnabled = Leg3LockEnabled,
                Leg4LockEnabled = Leg4LockEnabled,
                Leg1LockOptions = Leg1LockOptions,
                Leg2LockOptions = Leg2LockOptions,
                Leg3LockOptions = Leg3LockOptions,
                Leg4LockOptions = Leg4LockOptions,
                TopPercentageSelectionEnabled = TopPercentageSelectionEnabled,
                TopPercentageSelectionCount = TopPercentageSelectionCount,
                TopPercentageSelectionSortingMode = TopPercentageSelectionSortingMode,
                MaxCountEnabled = MaxCountEnabled,
                ParsedOutputCount = ParsedOutputCount,
                ParsedBuildCount = ParsedBuildCount,
                ParsedOutputEnabled = ParsedOutputEnabled,
                ApproximateMissingQuotes = ApproximateMissingQuotes,
                ApproximateMissingGreeks = ApproximateMissingGreeks,
                ApproximateMissingHanweck = ApproximateMissingHanweck,
                MaxCount = MaxCount,
                SingleLegEnabled = SingleLegEnabled,
                VerticalEnabled = VerticalEnabled,
                OneByTwoRatioEnabled = OneByTwoRatioEnabled,
                OneByThreeRatioEnabled = OneByThreeRatioEnabled,
                RatioEnabled = RatioEnabled,
                ButterflyEnabled = ButterflyEnabled,
                SkewedButterflyEnabled = SkewedButterflyEnabled,
                TreeEnabled = TreeEnabled,
                CalendarButterflyEnabled = CalendarButterflyEnabled,
                IronButterflyEnabled = IronButterflyEnabled,
                CalendarEnabled = CalendarEnabled,
                DiagonalEnabled = DiagonalEnabled,
                CondorEnabled = CondorEnabled,
                IronCondorEnabled = IronCondorEnabled,
                OneThreeThreeOneEnabled = OneThreeThreeOneEnabled,
                OneThreeTwoEnabled = OneThreeTwoEnabled,
                TwoThreeOneEnabled = TwoThreeOneEnabled,
                BoxEnabled = BoxEnabled,
                ExportToFile = ExportToFile,
                OpenInBasket = OpenInBasket,
                RunnerOption = RunnerOption,
                UnderlyingQuery = string.Join(",", underlyings.Distinct()),
                Leg1OpenInterestFilter = Leg1OpenInterestFilter,
                Leg2OpenInterestFilter = Leg2OpenInterestFilter,
                Leg3OpenInterestFilter = Leg3OpenInterestFilter,
                Leg4OpenInterestFilter = Leg4OpenInterestFilter,
                Leg1VolumeFilter = Leg1VolumeFilter,
                Leg2VolumeFilter = Leg2VolumeFilter,
                Leg3VolumeFilter = Leg3VolumeFilter,
                Leg4VolumeFilter = Leg4VolumeFilter,
                SingleLegSpreadsSettings = SingleLegSpreadsSettings.Clone(),
                VerticalSpreadsSettings = VerticalSpreadsSettings.Clone(),
                OneByTwoRatioSpreadsSettings = OneByTwoRatioSpreadsSettings.Clone(),
                OneByThreeRatioSpreadsSettings = OneByThreeRatioSpreadsSettings.Clone(),
                RatioSpreadsSettings = RatioSpreadsSettings.Clone(),
                CalendarSpreadsSettings = CalendarSpreadsSettings.Clone(),
                DiagonalSpreadsSettings = DiagonalSpreadsSettings.Clone(),
                ButterflySpreadsSettings = ButterflySpreadsSettings.Clone(),
                SkewedButterflySpreadsSettings = SkewedButterflySpreadsSettings.Clone(),
                TreeSpreadsSettings = TreeSpreadsSettings.Clone(),
                CalendarButterflySpreadsSettings = CalendarButterflySpreadsSettings.Clone(),
                IronButterflySpreadsSettings = IronButterflySpreadsSettings.Clone(),
                IronGutFlySpreadsSettings = IronGutFlySpreadsSettings.Clone(),
                CondorSpreadsSettings = CondorSpreadsSettings.Clone(),
                IronCondorSpreadsSettings = IronCondorSpreadsSettings.Clone(),
                OneThreeThreeOneSpreadsSettings = OneThreeThreeOneSpreadsSettings.Clone(),
                OneThreeTwoSpreadsSettings = OneThreeTwoSpreadsSettings.Clone(),
                TwoThreeOneSpreadsSettings = TwoThreeOneSpreadsSettings.Clone(),
                BoxSpreadsSettings = BoxSpreadsGeneratorSettings.Clone(),
            };
        }

        private List<string> GetUnderlyings(bool activeOnly)
        {
            List<string> underlyings = OptionChains.Select(x => x.Symbol).ToList();

            if (!activeOnly && !string.IsNullOrWhiteSpace(UnderlyingQuery))
            {
                List<string> symbols = UnderlyingQuery.Replace(",", ";")
                    .Split(';')
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpper())
                    .Select(x => OptionsHelper.IsIndex(x) ? "$" + x : x)
                    .Distinct()
                    .ToList();
                underlyings.AddRange(symbols);
            }

            return underlyings;
        }
        public void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex)
        {
            if (lastMessageIndex - orders.Count == 0)
            {
                lock (_bufferLock)
                {
                    _buffer.Clear();
                }
            }

            lock (_bufferLock)
            {
                foreach (IOrder order in orders)
                {
                    if (order != null)
                    {
                        _buffer.Enqueue(order);
                    }
                }
            }

            if (totalQueued == _buffer.Count)
            {
                List<IOrder> buffered;
                lock (_bufferLock)
                {
                    buffered = _buffer.ToList();
                    _buffer.Clear();
                }

                if (buffered != null)
                {
                    foreach (IOrder order in buffered)
                    {
                        ProcessOrder(order);
                    }
                }
                _waitForOrderLoadEvent.Set();
            }
        }

        private void ProcessOrder(IOrder order)
        {
            try
            {
                string underlying = order.UnderlyingSymbol;
                if (!_underlyingToFishedExpirationsMap.TryGetValue(underlying, out (HashSet<string> Puts, HashSet<string> Calls) expirations))
                {
                    expirations = new();
                    expirations.Puts = new HashSet<string>();
                    expirations.Calls = new HashSet<string>();
                    _underlyingToFishedExpirationsMap[underlying] = expirations;
                }

                if (order.IsComplexOrder && order is IComplexOrder complexOrder)
                {
                    foreach (IComplexOrderLeg leg in complexOrder.Legs)
                    {
                        if (OmsCore.SecurityBook.GetSecurity(leg.Symbol) is Option option)
                        {
                            switch (option.PutCall)
                            {
                                case PutCall.Put:
                                    expirations.Puts.Add(option.Symbol);
                                    break;
                                case PutCall.Call:
                                    expirations.Calls.Add(option.Symbol);
                                    break;
                            }
                        }
                    }
                }
                else if (!order.IsComplexOrder)
                {
                    if (OmsCore.SecurityBook.GetSecurity(order.Symbol) is Option option)
                    {
                        switch (option.PutCall)
                        {
                            case PutCall.Put:
                                expirations.Puts.Add(option.Symbol);
                                break;
                            case PutCall.Call:
                                expirations.Calls.Add(option.Symbol);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddMultipleOrders));
            }
        }
        public void AddMultiplePortfolios(HashSet<IPortfolio> portfolios)
        { }
    }
}
