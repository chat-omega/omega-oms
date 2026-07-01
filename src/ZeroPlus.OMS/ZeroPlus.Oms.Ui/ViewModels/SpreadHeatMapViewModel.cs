using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Charts;
using DevExpress.Xpf.Charts.Native;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{

    public partial class SpreadHeatmapViewModel : ModuleViewModelBase, IOrderArchiveReceiver
    {
        private readonly object _bufferLock = new();
        private readonly Queue<IOrder> _buffer = new();

        private readonly ConcurrentDictionary<string, SpreadHeatmapRowModel> _expirationToSpreadHeatmapRowMap = new();
        private readonly ConcurrentDictionary<string, int> _symbolToColMap = new();
        private readonly TransactionConsumerModel _transactionConsumerModel;
        private readonly PortfolioManagerModel _portfolioManagerModel;
        private readonly PortfolioManagerModel _archivePortfolioManagerModel;
        private int _nextColIndex;
        private DelegateCommand<object> _editAlertsCommand;
        private DelegateCommand<object> _showinOptionChainCommand;
        private List<IOrder> _buffered;
        private readonly DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopCollection _heatmapRangeStops;
        private readonly CustomPalette _heatmapColorPalette;
        private readonly CustomPalette _heatmapProfitColorPalette;
        private readonly NotificationManager _notificationManager;

        public bool _IsBusy;
        public DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopCollection _HeatmapRangeStops;
        public Palette _HeatmapColorPalette;
        public string _IsBusyMessage;
        public Color _MinColor;
        public Color _MaxColor;
        public Output _Output;
        public bool _HeatmapMode;
        public bool _OrderTallyMode;
        public IvChartType _ChartType;
        public bool _AlertEnabled;
        public string _OrderTallySymbol;
        public string _UnderlyingQuery;
        public string _HeaderCol1;
        public string _HeaderCol2;
        public string _HeaderCol3;
        public string _HeaderCol4;
        public string _HeaderCol5;
        public string _HeaderCol6;
        public string _HeaderCol7;
        public string _HeaderCol8;
        public string _HeaderCol9;
        public string _HeaderCol10;
        public string _HeaderCol11;
        public string _HeaderCol12;
        public string _HeaderCol13;
        public string _HeaderCol14;
        public string _HeaderCol15;
        public string _HeaderCol16;
        public string _HeaderCol17;
        public string _HeaderCol18;
        public string _HeaderCol19;
        public string _HeaderCol20;
        public string _HeaderCol21;
        public string _HeaderCol22;
        public string _HeaderCol23;
        public string _HeaderCol24;
        public string _HeaderCol25;
        public string _HeaderCol26;
        public string _HeaderCol27;
        public string _HeaderCol28;
        public string _HeaderCol29;
        public string _HeaderCol30;
        public string _HeaderCol31;
        public string _HeaderCol32;
        public string _HeaderCol33;
        public string _HeaderCol34;
        public string _HeaderCol35;
        public string _HeaderCol36;
        public string _HeaderCol37;
        public string _HeaderCol38;
        public string _HeaderCol39;
        public string _HeaderCol40;
        public string _HeaderCol41;
        public string _HeaderCol42;
        public string _HeaderCol43;
        public string _HeaderCol44;
        public string _HeaderCol45;
        public string _HeaderCol46;
        public string _HeaderCol47;
        public string _HeaderCol48;
        public string _HeaderCol49;
        public string _HeaderCol50;
        public bool _HeaderColVisible1;
        public bool _HeaderColVisible2;
        public bool _HeaderColVisible3;
        public bool _HeaderColVisible4;
        public bool _HeaderColVisible5;
        public bool _HeaderColVisible6;
        public bool _HeaderColVisible7;
        public bool _HeaderColVisible8;
        public bool _HeaderColVisible9;
        public bool _HeaderColVisible10;
        public bool _HeaderColVisible11;
        public bool _HeaderColVisible12;
        public bool _HeaderColVisible13;
        public bool _HeaderColVisible14;
        public bool _HeaderColVisible15;
        public bool _HeaderColVisible16;
        public bool _HeaderColVisible17;
        public bool _HeaderColVisible18;
        public bool _HeaderColVisible19;
        public bool _HeaderColVisible20;
        public bool _HeaderColVisible21;
        public bool _HeaderColVisible22;
        public bool _HeaderColVisible23;
        public bool _HeaderColVisible24;
        public bool _HeaderColVisible25;
        public bool _HeaderColVisible26;
        public bool _HeaderColVisible27;
        public bool _HeaderColVisible28;
        public bool _HeaderColVisible29;
        public bool _HeaderColVisible30;
        public bool _HeaderColVisible31;
        public bool _HeaderColVisible32;
        public bool _HeaderColVisible33;
        public bool _HeaderColVisible34;
        public bool _HeaderColVisible35;
        public bool _HeaderColVisible36;
        public bool _HeaderColVisible37;
        public bool _HeaderColVisible38;
        public bool _HeaderColVisible39;
        public bool _HeaderColVisible40;
        public bool _HeaderColVisible41;
        public bool _HeaderColVisible42;
        public bool _HeaderColVisible43;
        public bool _HeaderColVisible44;
        public bool _HeaderColVisible45;
        public bool _HeaderColVisible46;
        public bool _HeaderColVisible47;
        public bool _HeaderColVisible48;
        public bool _HeaderColVisible49;
        public bool _HeaderColVisible50;
        public ObservableCollection<OptionChainModel> _OptionChains;
        public ObservableCollection<SpreadHeatmapRowModel> _HeatmapRows;
        public double[] _XArguments;
        public string[] _YArguments;
        public double[,] _TallyValues;
        public object _SelectedTallyCell;


        public override Module Module { get; protected set; } = Module.Heatmap;

        public List<HeatMapMode> HeatMapModes { get; } = Enum.GetValues(typeof(HeatMapMode)).Cast<HeatMapMode>().ToList();
        public List<Operator> Operators { get; } = Enum.GetValues(typeof(Operator)).Cast<Operator>().ToList();
        public List<Output> Outputs { get; } = Enum.GetValues(typeof(Output)).Cast<Output>().ToList();
        public List<IvChartType> ChartTypes { get; } = Enum.GetValues(typeof(IvChartType)).Cast<IvChartType>().ToList();
        public List<UnderPriceSource> UnderPriceSources { get; } = Enum.GetValues(typeof(UnderPriceSource)).Cast<UnderPriceSource>().ToList();

        public ConcurrentDictionary<string, SpreadHeatmapAlert> GroupHeaderToGroupAlertMap { get; set; } = new ConcurrentDictionary<string, SpreadHeatmapAlert>();
        public HeatmapSettingsModel GlobalHeatmapSettingsModel { get; set; }
        public List<HeatmapSettingsModel> UnderlyngSettings { get; set; } = new List<HeatmapSettingsModel>();

        public static SolidColorBrush Brush10 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#84cee9");
        public static SolidColorBrush Brush20 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#61bfe3");
        public static SolidColorBrush Brush30 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#3eb1de");
        public static SolidColorBrush Brush40 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#239fd1");
        public static SolidColorBrush Brush50 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#1d86ae");
        public static SolidColorBrush Brush60 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#186b8d");
        public static SolidColorBrush Brush70 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#125069");
        public static SolidColorBrush Brush80 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#0c3646");
        public static SolidColorBrush Brush90 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#061b24");
        public static SolidColorBrush Brush100 { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#030c11");

        [Bindable]
        public partial bool IsBusy { get; set; }

        [Bindable]
        public partial DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopCollection HeatmapRangeStops { get; set; }

        [Bindable]
        public partial Palette HeatmapColorPalette { get; set; }

        [Bindable]
        public partial string IsBusyMessage { get; set; }

        public Color MinColor
        {
            get => _MinColor;
            set
            {
                SetValue(ref _MinColor, value);
                UpdateColorRange();
            }
        }

        public Color MaxColor
        {
            get => _MaxColor;
            set
            {
                SetValue(ref _MaxColor, value);
                UpdateColorRange();
            }
        }

        public Output Output
        {
            get => _Output;
            set
            {
                SetValue(ref _Output, value);
                switch (value)
                {
                    case Output.Heatmap:
                        HeatmapMode = true;
                        OrderTallyMode = false;
                        break;
                    case Output.Moneyness:
                    case Output.Profitability:
                        HeatmapMode = false;
                        OrderTallyMode = true;
                        break;
                }
            }
        }

        [Bindable]
        public partial bool HeatmapMode { get; set; }

        [Bindable]
        public partial bool OrderTallyMode { get; set; }

        [Bindable]
        public partial IvChartType ChartType { get; set; }

        [Bindable]
        public partial bool AlertEnabled { get; set; }

        public string OrderTallySymbol
        {
            get => _OrderTallySymbol;
            set => SetValue(ref _OrderTallySymbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        public string UnderlyingQuery
        {
            get => _UnderlyingQuery;
            set => SetValue(ref _UnderlyingQuery, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        [Bindable]
        public partial string HeaderCol1 { get; set; }
        [Bindable]
        public partial string HeaderCol2 { get; set; }
        [Bindable]
        public partial string HeaderCol3 { get; set; }
        [Bindable]
        public partial string HeaderCol4 { get; set; }
        [Bindable]
        public partial string HeaderCol5 { get; set; }
        [Bindable]
        public partial string HeaderCol6 { get; set; }
        [Bindable]
        public partial string HeaderCol7 { get; set; }
        [Bindable]
        public partial string HeaderCol8 { get; set; }
        [Bindable]
        public partial string HeaderCol9 { get; set; }
        [Bindable]
        public partial string HeaderCol10 { get; set; }
        [Bindable]
        public partial string HeaderCol11 { get; set; }
        [Bindable]
        public partial string HeaderCol12 { get; set; }
        [Bindable]
        public partial string HeaderCol13 { get; set; }
        [Bindable]
        public partial string HeaderCol14 { get; set; }
        [Bindable]
        public partial string HeaderCol15 { get; set; }
        [Bindable]
        public partial string HeaderCol16 { get; set; }
        [Bindable]
        public partial string HeaderCol17 { get; set; }
        [Bindable]
        public partial string HeaderCol18 { get; set; }
        [Bindable]
        public partial string HeaderCol19 { get; set; }
        [Bindable]
        public partial string HeaderCol20 { get; set; }
        [Bindable]
        public partial string HeaderCol21 { get; set; }
        [Bindable]
        public partial string HeaderCol22 { get; set; }
        [Bindable]
        public partial string HeaderCol23 { get; set; }
        [Bindable]
        public partial string HeaderCol24 { get; set; }
        [Bindable]
        public partial string HeaderCol25 { get; set; }
        [Bindable]
        public partial string HeaderCol26 { get; set; }
        [Bindable]
        public partial string HeaderCol27 { get; set; }
        [Bindable]
        public partial string HeaderCol28 { get; set; }
        [Bindable]
        public partial string HeaderCol29 { get; set; }
        [Bindable]
        public partial string HeaderCol30 { get; set; }
        [Bindable]
        public partial string HeaderCol31 { get; set; }
        [Bindable]
        public partial string HeaderCol32 { get; set; }
        [Bindable]
        public partial string HeaderCol33 { get; set; }
        [Bindable]
        public partial string HeaderCol34 { get; set; }
        [Bindable]
        public partial string HeaderCol35 { get; set; }
        [Bindable]
        public partial string HeaderCol36 { get; set; }
        [Bindable]
        public partial string HeaderCol37 { get; set; }
        [Bindable]
        public partial string HeaderCol38 { get; set; }
        [Bindable]
        public partial string HeaderCol39 { get; set; }
        [Bindable]
        public partial string HeaderCol40 { get; set; }
        [Bindable]
        public partial string HeaderCol41 { get; set; }
        [Bindable]
        public partial string HeaderCol42 { get; set; }
        [Bindable]
        public partial string HeaderCol43 { get; set; }
        [Bindable]
        public partial string HeaderCol44 { get; set; }
        [Bindable]
        public partial string HeaderCol45 { get; set; }
        [Bindable]
        public partial string HeaderCol46 { get; set; }
        [Bindable]
        public partial string HeaderCol47 { get; set; }
        [Bindable]
        public partial string HeaderCol48 { get; set; }
        [Bindable]
        public partial string HeaderCol49 { get; set; }
        [Bindable]
        public partial string HeaderCol50 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible1 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible2 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible3 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible4 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible5 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible6 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible7 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible8 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible9 { get; set; }

        [Bindable]
        public partial bool HeaderColVisible10 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible11 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible12 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible13 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible14 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible15 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible16 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible17 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible18 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible19 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible20 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible21 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible22 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible23 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible24 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible25 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible26 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible27 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible28 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible29 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible30 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible31 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible32 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible33 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible34 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible35 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible36 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible37 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible38 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible39 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible40 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible41 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible42 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible43 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible44 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible45 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible46 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible47 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible48 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible49 { get; set; }
        [Bindable]
        public partial bool HeaderColVisible50 { get; set; }

        [Bindable]
        public partial ObservableCollection<OptionChainModel> OptionChains { get; set; }

        [Bindable]
        public partial ObservableCollection<SpreadHeatmapRowModel> HeatmapRows { get; set; }

        [Bindable]
        public partial double[] XArguments { get; set; }

        [Bindable]
        public partial string[] YArguments { get; set; }

        [Bindable]
        public partial double[,] TallyValues { get; set; }

        [Bindable]
        public partial object SelectedTallyCell { get; set; }

        public ICommand EditAlertsCommand
        {
            get
            {
                _editAlertsCommand ??= new DelegateCommand<object>(EditAlerts);

                return _editAlertsCommand;
            }
        }

        public ICommand ShowinOptionChainCommand
        {
            get
            {
                _showinOptionChainCommand ??= new DelegateCommand<object>(ShowinOptionChain);

                return _showinOptionChainCommand;
            }
        }


        public SpreadHeatmapViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, TransactionConsumerModel transactionConsumerModel, PortfolioManagerModel portfolioManagerModel, NotificationManager notificationManagerModel) : base(configBrowserViewModel, omsCore)
        {
            _notificationManager = notificationManagerModel;
            _transactionConsumerModel = transactionConsumerModel;
            _portfolioManagerModel = portfolioManagerModel;
            _archivePortfolioManagerModel = new PortfolioManagerModel(_notificationManager)
            {
                OmsCore = omsCore,
            };
            GlobalHeatmapSettingsModel = new HeatmapSettingsModel(25, _notificationManager);
            ModuleTitle = "Heatmap";
            Output = Output.Heatmap;
            HeatmapRows = new ObservableCollection<SpreadHeatmapRowModel>();
            OptionChains = new ObservableCollection<OptionChainModel>();
            MinColor = (Color)ColorConverter.ConvertFromString("#84CEE9");
            MaxColor = (Color)ColorConverter.ConvertFromString("#030C11");

            Brush10 = (SolidColorBrush)new BrushConverter().ConvertFrom("#84cee9");
            Brush20 = (SolidColorBrush)new BrushConverter().ConvertFrom("#61bfe3");
            Brush30 = (SolidColorBrush)new BrushConverter().ConvertFrom("#3eb1de");
            Brush40 = (SolidColorBrush)new BrushConverter().ConvertFrom("#239fd1");
            Brush50 = (SolidColorBrush)new BrushConverter().ConvertFrom("#1d86ae");
            Brush60 = (SolidColorBrush)new BrushConverter().ConvertFrom("#186b8d");
            Brush70 = (SolidColorBrush)new BrushConverter().ConvertFrom("#125069");
            Brush80 = (SolidColorBrush)new BrushConverter().ConvertFrom("#0c3646");
            Brush90 = (SolidColorBrush)new BrushConverter().ConvertFrom("#061b24");
            Brush100 = (SolidColorBrush)new BrushConverter().ConvertFrom("#030c11");

            _heatmapRangeStops = new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopCollection
            {
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Absolute),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(1, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Absolute),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.0, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.05, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.1, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.15, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.2, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.25, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.3, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.35, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.4, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.45, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.5, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.55, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.6, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.65, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.7, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.75, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.8, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.85, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.9, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0.95, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage),
                new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(1.0, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Percentage)
            };
            HeatmapRangeStops = _heatmapRangeStops;

            Color[] colors = new Color[]
            {
                (Color)ColorConverter.ConvertFromString("#f8faff"),
                (Color)ColorConverter.ConvertFromString("#ff0000"),
            };
            _heatmapColorPalette = new CustomPalette(colors);
            HeatmapColorPalette = _heatmapColorPalette;

            Color[] profitColors = new Color[]
            {
                (Color)ColorConverter.ConvertFromString("#ff0000"),
                (Color)ColorConverter.ConvertFromString("#ffffff"),
                (Color)ColorConverter.ConvertFromString("#004d00"),
            };
            _heatmapProfitColorPalette = new CustomPalette(profitColors);

            XArguments = Array.Empty<double>();
            YArguments = Array.Empty<string>();
            TallyValues = new double[0, 0];
        }

        [Command]
        public void Clone()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Heatmap))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        SpreadHeatmapView window = new();
                        SpreadHeatmapViewModel viewModel = (SpreadHeatmapViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) => viewModel.LoadFromConfig(GetConfig());

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }

        [Command]
        public void CustomColumnSort(RowSortArgs args)
        {
            SpreadHeatmapRowModel firstValue = args.FirstItem as SpreadHeatmapRowModel;
            SpreadHeatmapRowModel secondValue = args.SecondItem as SpreadHeatmapRowModel;
            if (firstValue.ExpirationDateTime == secondValue.ExpirationDateTime)
            {
                args.Result = 0;
            }
            else
            {
                args.Result = firstValue.ExpirationDateTime > secondValue.ExpirationDateTime ? 1 : -1;
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
                    await Task.Run(async () =>
                    {
                        IEnumerable<string> symbols = UnderlyingQuery.Replace(",", ";")
                                                     .Split(';')
                                                     .Where(x => !string.IsNullOrWhiteSpace(x))
                                                     .Select(x => x.Trim().ToUpper())
                                                     .Select(x => OptionsHelper.IsIndex(x) ? "$" + x : x)
                                                     .Distinct();

                        HashSet<string> dict = OptionChains.Select(x => x.Symbol).ToHashSet();
                        IEnumerable<string> newItems = symbols.Where(x => !dict.Contains(x));

                        List<Task<List<Option>>> getOptionsTasks = new();
                        foreach (string symbol in newItems)
                        {
                            getOptionsTasks.Add(OmsCore.QuoteClient.GetSymbols(symbol));
                        }
                        await Task.WhenAll(getOptionsTasks);
                        List<OptionChainModel> results = getOptionsTasks.Where(x => !x.IsFaulted).Select(x => new OptionChainModel(OmsCore.SecurityBook, x.Result)).ToList();
                        AddMultipleOptionChains(results);
                    });
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
                Dispatcher?.Invoke(() =>
                {
                    OptionChains.Remove(optionChain);
                    if (_symbolToColMap.TryGetValue(optionChain.Symbol, out int col))
                    {
                        foreach (SpreadHeatmapRowModel row in HeatmapRows.ToList())
                        {
                            row.RemoveCol(col);
                            if (row.IsEmpty())
                            {
                                HeatmapRows.Remove(row);
                                _expirationToSpreadHeatmapRowMap.TryRemove(row.Expiration, out _);
                            }
                        }
                        GlobalHeatmapSettingsModel.RemoveGroupFromTop(optionChain.Symbol);
                        SetColumns(col, string.Empty, false);
                        GlobalHeatmapSettingsModel.Reset(false);
                    }
                });
            }
        }

        [Command]
        public void ClearAllAlerts()
        {
            foreach (SpreadHeatmapRowModel map in HeatmapRows)
            {
                map.ClearAlerts();
            }
        }

        [Command]
        public void HeatMapModeChanged()
        {
            ResetAll();
        }

        [Command]
        public void RefreshCommand()
        {
            switch (Output)
            {
                case Output.Moneyness:
                case Output.Profitability:
                    RefreshOrderTally();
                    break;
                case Output.Heatmap:
                    break;
            }
        }

        [Command]
        public void CancelCommand()
        {
            IsBusy = false;
            switch (Output)
            {
                case Output.Heatmap:
                    GlobalHeatmapSettingsModel.Reset(false);
                    break;
                case Output.Moneyness:
                case Output.Profitability:
                    ClearHeatmap();
                    break;
            }
        }

        private void RefreshOrderTally()
        {
            try
            {
                IsBusy = true;
                ClearHeatmap();
                if (string.IsNullOrEmpty(OrderTallySymbol))
                {
                    return;
                }
                TimeSpan days = TimeSpan.FromDays(GlobalHeatmapSettingsModel.TotalDays);
                TimeSpan mins = TimeSpan.FromMinutes(GlobalHeatmapSettingsModel.TotalMins);
                DateTime endDateTime = DateTime.Now;
                DateTime startDateTime = endDateTime - days - mins;
                List<string> underlyings = new() { OrderTallySymbol };
                List<OrderStatus> orderStatus = Output == Output.Moneyness ? new List<OrderStatus>() { OrderStatus.New } : new List<OrderStatus>() { OrderStatus.PartiallyFilled, OrderStatus.Filled };
                int requestId = OmsCore.HerculesClient.RequestTransactionsFromArchive(startDateTime, endDateTime, ordersOnly: Output == Output.Moneyness, orderStatus, new List<string>(), new List<string>(), new List<string>(), underlyings);
                _transactionConsumerModel.AddRequester(requestId, this);
                _portfolioManagerModel.AddRequester(requestId, this);
            }
            catch (Exception ex)
            {
                IsBusy = false;
                MessageBoxService.ShowMessage("Failed to refresh heatmap.\n" + ex.Message, "Heatmap - ZeroPlus OMS", MessageButton.OK, MessageIcon.Error);
                _log.Error(ex, nameof(RefreshOrderTally));
            }
        }

        private void ClearHeatmap()
        {
            XArguments = Array.Empty<double>();
            YArguments = Array.Empty<string>();
            TallyValues = new double[0, 0];
        }

        public void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex)
        {
            if (orders != null)
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
                    lock (_bufferLock)
                    {
                        _buffered = _buffer.ToList();
                        _buffer.Clear();
                    }

                    if (_buffered != null && Output == Output.Moneyness)
                    {
                        Task.Run(() => ProcessOrdersForTally(orders));
                    }
                    else if (_buffered != null && Output == Output.Profitability)
                    {
                        Task.Run(() => ProcessOrdersForProfitTally(_buffered));
                    }
                    else
                    {
                        IsBusy = false;
                    }
                }
            }
        }

        public void AddMultiplePortfolios(HashSet<IPortfolio> portfolios)
        {
            _archivePortfolioManagerModel.MultiplePortfoliosAdded(0, portfolios);
        }

        private async void ProcessOrdersForTally(List<IOrder> orders)
        {
            try
            {
                if (orders == null || orders.Count == 0)
                {
                    IsBusy = false;
                    Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Failed to refresh heatmap.\nNo orders found.", "Heatmap - ZeroPlus OMS", MessageButton.OK, MessageIcon.Error));
                    return;
                }

                List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(OrderTallySymbol);

                if (!GlobalHeatmapSettingsModel.PutSelected)
                {
                    options = options?.Where(x => x.Type != OptionType.PUT).ToList();
                }

                if (!GlobalHeatmapSettingsModel.CallSelected)
                {
                    options = options?.Where(x => x.Type != OptionType.CALL).ToList();
                }

                if (options == null || options.Count == 0)
                {
                    IsBusy = false;
                    Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Failed to refresh heatmap.\nNo symbols found.", "Heatmap - ZeroPlus OMS", MessageButton.OK, MessageIcon.Error));
                    return;
                }

                double lastPrice = await OmsCore.QuoteClient.GetSnapshotAsync(OrderTallySymbol, SubscriptionFieldType.LastPrice);
                int digits = lastPrice > 1000 ? 4 : 3;
                Dictionary<DateTime, Dictionary<double, int>> dictionary = new();
                foreach (IOrder order in orders)
                {
                    if (order.IsComplexOrder && order is IComplexOrder complexOrder)
                    {
                        foreach (IComplexOrderLeg leg in complexOrder.Legs)
                        {
                            Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);

                            switch (option.Type)
                            {
                                case OptionType.PUT:
                                    if (!GlobalHeatmapSettingsModel.PutSelected)
                                    {
                                        continue;
                                    }
                                    break;
                                case OptionType.CALL:
                                    if (!GlobalHeatmapSettingsModel.CallSelected)
                                    {
                                        continue;
                                    }
                                    break;
                            }

                            double moneyness = Math.Round(1 - (option.Strike / lastPrice), digits);
                            Tuple<DateTime, double> key = Tuple.Create(option.Expiration.Date, moneyness);

                            if (!dictionary.TryGetValue(option.Expiration.Date, out Dictionary<double, int> strikeToMoneynessMap))
                            {
                                strikeToMoneynessMap = new Dictionary<double, int>();
                                dictionary[option.Expiration.Date] = strikeToMoneynessMap;
                            }

                            strikeToMoneynessMap.TryGetValue(moneyness, out int count);
                            strikeToMoneynessMap[moneyness] = count + 1;
                        }
                    }
                    else if (!order.IsComplexOrder)
                    {
                        Option option = OptionsHelper.GetOptionFromSymbol(order.Symbol);

                        switch (option.Type)
                        {
                            case OptionType.PUT:
                                if (!GlobalHeatmapSettingsModel.PutSelected)
                                {
                                    continue;
                                }
                                break;
                            case OptionType.CALL:
                                if (!GlobalHeatmapSettingsModel.CallSelected)
                                {
                                    continue;
                                }
                                break;
                        }

                        double moneyness = Math.Round(1 - (option.Strike / lastPrice), digits);
                        Tuple<DateTime, double> key = Tuple.Create(option.Expiration.Date, moneyness);

                        if (!dictionary.TryGetValue(option.Expiration.Date, out Dictionary<double, int> moneynessToCountMap))
                        {
                            moneynessToCountMap = new Dictionary<double, int>();
                            dictionary[option.Expiration.Date] = moneynessToCountMap;
                        }

                        moneynessToCountMap.TryGetValue(moneyness, out int count);
                        moneynessToCountMap[moneyness] = count + 1;
                    }
                }

                List<double> strikes = options.Select(x => x.Strike).Distinct().OrderBy(x => x).ToList();
                List<DateTime> expirations = options.Select(x => x.Expiration.Date).Distinct().OrderBy(x => x).ToList();
                Dictionary<DateTime, HashSet<double>> expirationToStrikesMap = options.GroupBy(x => x.Expiration.Date).ToDictionary(x => x.Key, y => y.Select(x => x.Strike).ToHashSet());

                foreach (DateTime expiration in expirations)
                {
                    if (!dictionary.TryGetValue(expiration, out Dictionary<double, int> moneynessToCountMap))
                    {
                        moneynessToCountMap = new Dictionary<double, int>();
                        dictionary[expiration] = moneynessToCountMap;
                    }
                }

                double maxPercent = Math.Round(1 - (strikes.Min() / lastPrice), digits);
                double minPercent = Math.Round(1 - (strikes.Max() / lastPrice), digits);
                double[] rangeValues = Range(minPercent, maxPercent, Math.Pow(.1, digits), digits).OrderBy(x => x).ToArray();

                foreach (DateTime expiration in dictionary.Keys)
                {
                    Dictionary<double, int> moneynessToCountMap = dictionary[expiration];
                    double[] validMoneynessValues = moneynessToCountMap.Select(x => x.Key).ToArray();
                    for (int i = 0; i < rangeValues.Length; i++)
                    {
                        double percent = rangeValues[i];
                        if (!moneynessToCountMap.TryGetValue(percent, out int count))
                        {
                            if (validMoneynessValues.Length > 0)
                            {
                                double closestValue = validMoneynessValues.MinBy(x => x - percent);
                                moneynessToCountMap[percent] = moneynessToCountMap[closestValue];
                            }
                            else
                            {
                                moneynessToCountMap[percent] = 0;
                            }
                        }
                    }
                }

                string[] yArgs = expirations.Select(x => x.ToString("MMM dd yy")).ToArray();
                double[] xArgs = strikes.ToArray();

                double[,] tallyValues = new double[yArgs.Length, xArgs.Length];
                for (int i = 0; i < yArgs.Length; i++)
                {
                    DateTime expiration = expirations[i];
                    if (dictionary.TryGetValue(expiration, out Dictionary<double, int> moneynessToCountMap) && expirationToStrikesMap.TryGetValue(expiration.Date, out HashSet<double> strikesList))
                    {
                        int max = moneynessToCountMap.Values.Max();
                        if (max > 0)
                        {
                            for (int j = 0; j < xArgs.Length; j++)
                            {
                                double strike = xArgs[j];
                                if (strikesList.Contains(strike))
                                {
                                    double moneyness = Math.Round(1 - (strike / lastPrice), digits);
                                    if (moneynessToCountMap.TryGetValue(moneyness, out int count))
                                    {
                                        tallyValues[i, j] = count;
                                    }
                                }
                                else
                                {
                                    tallyValues[i, j] = double.NaN;
                                }
                            }
                        }
                    }
                }

                Dispatcher?.BeginInvoke(() =>
                {
                    HeatmapRangeStops = _heatmapRangeStops;
                    HeatmapColorPalette = _heatmapColorPalette;
                    TallyValues = tallyValues;
                    YArguments = yArgs;
                    XArguments = xArgs;
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessOrdersForTally));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ProcessOrdersForProfitTally(List<IOrder> orders)
        {
            try
            {
                if (orders == null || orders.Count == 0)
                {
                    IsBusy = false;
                    Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Failed to refresh heatmap.\nNo orders found.", "Heatmap - ZeroPlus OMS", MessageButton.OK, MessageIcon.Error));
                    return;
                }

                List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(OrderTallySymbol);

                if (!GlobalHeatmapSettingsModel.PutSelected)
                {
                    options = options?.Where(x => x.Type != OptionType.PUT).ToList();
                }

                if (!GlobalHeatmapSettingsModel.CallSelected)
                {
                    options = options?.Where(x => x.Type != OptionType.CALL).ToList();
                }

                if (options == null || options.Count == 0)
                {
                    IsBusy = false;
                    Dispatcher?.BeginInvoke(() => MessageBoxService.ShowMessage("Failed to refresh heatmap.\nNo symbols found.", "Heatmap - ZeroPlus OMS", MessageButton.OK, MessageIcon.Error));
                    return;
                }

                Dictionary<DateTime, Dictionary<double, double>> dictionary = new();
                foreach (OmsOrderModel order in orders.GroupBy(x => x.SpreadId).Where(x => x.Any()).Select(x => x.FirstOrDefault()))
                {
                    _archivePortfolioManagerModel.Subscribe(order.SpreadId, SubscriptionFieldType.FirmSpreadPosition, order);

                    if (order.IsComplexOrder && order is IComplexOrder complexOrder)
                    {
                        foreach (IComplexOrderLeg leg in complexOrder.Legs)
                        {
                            Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);

                            switch (option.Type)
                            {
                                case OptionType.PUT:
                                    if (!GlobalHeatmapSettingsModel.PutSelected)
                                    {
                                        continue;
                                    }
                                    break;
                                case OptionType.CALL:
                                    if (!GlobalHeatmapSettingsModel.CallSelected)
                                    {
                                        continue;
                                    }
                                    break;
                            }

                            double strike = option.Strike;
                            Tuple<DateTime, double> key = Tuple.Create(option.Expiration.Date, strike);

                            if (!dictionary.TryGetValue(option.Expiration.Date, out Dictionary<double, double> strikeToProfitMap))
                            {
                                strikeToProfitMap = new Dictionary<double, double>();
                                dictionary[option.Expiration.Date] = strikeToProfitMap;
                            }

                            strikeToProfitMap.TryGetValue(strike, out double prev);
                            strikeToProfitMap[strike] = prev + order.AdjustedPnl;
                        }
                    }
                    else if (!order.IsComplexOrder)
                    {
                        Option option = OptionsHelper.GetOptionFromSymbol(order.Symbol);

                        switch (option.Type)
                        {
                            case OptionType.PUT:
                                if (!GlobalHeatmapSettingsModel.PutSelected)
                                {
                                    continue;
                                }
                                break;
                            case OptionType.CALL:
                                if (!GlobalHeatmapSettingsModel.CallSelected)
                                {
                                    continue;
                                }
                                break;
                        }

                        double strike = option.Strike;
                        Tuple<DateTime, double> key = Tuple.Create(option.Expiration.Date, strike);

                        if (!dictionary.TryGetValue(option.Expiration.Date, out Dictionary<double, double> strikeToProfitMap))
                        {
                            strikeToProfitMap = new Dictionary<double, double>();
                            dictionary[option.Expiration.Date] = strikeToProfitMap;
                        }

                        strikeToProfitMap.TryGetValue(strike, out double prev);
                        strikeToProfitMap[strike] = prev + order.AdjustedPnl;
                    }
                }

                List<DateTime> expirations = options.Select(x => x.Expiration.Date).Distinct().OrderBy(x => x).ToList();
                foreach (DateTime expiration in expirations)
                {
                    if (!dictionary.TryGetValue(expiration, out Dictionary<double, double> strikeToPnlMap))
                    {
                        strikeToPnlMap = new Dictionary<double, double>();
                        dictionary[expiration] = strikeToPnlMap;
                    }
                }

                List<double> strikes = options.Select(x => x.Strike).Distinct().OrderBy(x => x).ToList();

                string[] yArgs = expirations.Select(x => x.ToString("MMM dd yy")).ToArray();
                double[] xArgs = strikes.ToArray();

                double[,] tallyValues = new double[yArgs.Length, xArgs.Length];
                for (int i = 0; i < yArgs.Length; i++)
                {
                    DateTime expiration = expirations[i];
                    Dictionary<DateTime, HashSet<double>> expirationToStrikesMap = options.GroupBy(x => x.Expiration.Date).ToDictionary(x => x.Key, y => y.Select(x => x.Strike).ToHashSet());
                    if (dictionary.TryGetValue(expiration, out Dictionary<double, double> moneynessToCountMap) && expirationToStrikesMap.TryGetValue(expiration.Date, out HashSet<double> strikesList))
                    {
                        if (moneynessToCountMap.Count > 0)
                        {
                            double max = moneynessToCountMap.Values.Max();
                            if (max > 0)
                            {
                                for (int j = 0; j < xArgs.Length; j++)
                                {
                                    double strike = xArgs[j];
                                    if (strikesList.Contains(strike))
                                    {
                                        double moneyness = strike;
                                        if (moneynessToCountMap.TryGetValue(moneyness, out double prev))
                                        {
                                            tallyValues[i, j] = prev;
                                        }
                                    }
                                    else
                                    {
                                        tallyValues[i, j] = double.NaN;
                                    }
                                }
                            }
                        }
                    }
                }

                double minPnl = orders.Min(x => ((OmsOrderModel)x).AdjustedPnl);
                double maxPnl = orders.Max(x => ((OmsOrderModel)x).AdjustedPnl);

                double maxV = Math.Max(Math.Abs(minPnl), Math.Abs(maxPnl));

                minPnl = -Math.Abs(maxV);
                maxPnl = Math.Abs(maxV);

                int points = 20;
                int mid = points / 2;

                DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopCollection heatmapRangeStops = new();
                double interval = maxPnl / mid;
                for (int i = 0; i <= points; i++)
                {
                    if (i == mid)
                    {
                        heatmapRangeStops.Add(new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Absolute));
                        heatmapRangeStops.Add(new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(0, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Absolute));
                    }
                    else
                    {
                        double value = minPnl + (i * interval);
                        heatmapRangeStops.Add(new DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStop(value, DevExpress.Xpf.Charts.Heatmap.HeatmapRangeStopType.Absolute));
                    }
                }

                Dispatcher?.BeginInvoke(() =>
                {
                    HeatmapRangeStops = heatmapRangeStops;
                    HeatmapColorPalette = _heatmapProfitColorPalette;
                    TallyValues = tallyValues;
                    YArguments = yArgs;
                    XArguments = xArgs;
                });
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessOrdersForProfitTally));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public static IEnumerable<double> Range(double start, double stop, double step = 1, int digits = 3)
        {
            stop += step;
            if (start < stop && step > 0)
            {
                for (double i = start; i < stop; i += step)
                {
                    yield return Math.Round(i, digits);
                }
            }
            else if (start > stop && step < 0)
            {
                for (double i = start; i > stop; i += step)
                {
                    yield return Math.Round(i, digits);
                }
            }
        }

        private void ResetAll()
        {
            GlobalHeatmapSettingsModel.Reset(false);
            foreach (HeatmapSettingsModel groupSetting in UnderlyngSettings)
            {
                groupSetting.Reset(false);
            }
        }

        private void UpdateColorRange()
        {
            List<Color> gradients = GetGradients(MinColor, MaxColor, 8);

            Brush10 = new SolidColorBrush(MinColor);
            Brush20 = new SolidColorBrush(gradients[0]);
            Brush30 = new SolidColorBrush(gradients[1]);
            Brush40 = new SolidColorBrush(gradients[2]);
            Brush50 = new SolidColorBrush(gradients[3]);
            Brush60 = new SolidColorBrush(gradients[4]);
            Brush70 = new SolidColorBrush(gradients[5]);
            Brush80 = new SolidColorBrush(gradients[6]);
            Brush90 = new SolidColorBrush(gradients[7]);
            Brush100 = new SolidColorBrush(MaxColor);

            foreach (SpreadHeatmapRowModel row in HeatmapRows)
            {
                row.Update();
            }
        }

        public static List<Color> GetGradients(Color start, Color end, int steps)
        {
            List<Color> gradients = new();
            int stepA = (end.A - start.A) / (steps - 1);
            int stepR = (end.R - start.R) / (steps - 1);
            int stepG = (end.G - start.G) / (steps - 1);
            int stepB = (end.B - start.B) / (steps - 1);

            for (int i = 0; i < steps; i++)
            {
                gradients.Add(Color.FromArgb((byte)(start.A + (stepA * i)),
                                            (byte)(start.R + (stepR * i)),
                                            (byte)(start.G + (stepG * i)),
                                            (byte)(start.B + (stepB * i))));
            }
            return gradients;
        }

        private void AddMultipleOptionChains(List<OptionChainModel> optionChains)
        {
            List<SpreadHeatmapRowModel> heatmapRows = new();
            List<HeatmapSettingsModel> underlyngSettings = new();
            foreach (OptionChainModel optionChain in optionChains)
            {
                _nextColIndex++;
                _symbolToColMap[optionChain.Symbol] = _nextColIndex;
                SetColumns(_nextColIndex, optionChain.Symbol, true);

                if (!GroupHeaderToGroupAlertMap.TryGetValue(optionChain.Symbol, out SpreadHeatmapAlert groupAlert))
                {
                    groupAlert = new SpreadHeatmapAlert();
                    GroupHeaderToGroupAlertMap[optionChain.Symbol] = groupAlert;
                }

                HeatmapSettingsModel heatmapSettingsModel = new();
                underlyngSettings.Add(heatmapSettingsModel);
                DataStore deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                deltaStore.GetHanweckDataFor(optionChain.OptionChain, SubscriptionFieldType.Delta);
                foreach (DateTime optionExpiration in optionChain.OptionChain.Select(x => x.Expiration).Distinct())
                {
                    string expirationString = optionExpiration.ToString("MMM-dd-yy tt").ToUpper();
                    if (!_expirationToSpreadHeatmapRowMap.TryGetValue(expirationString, out SpreadHeatmapRowModel spreadHeatmapRow))
                    {
                        spreadHeatmapRow = new SpreadHeatmapRowModel(_notificationManager)
                        {
                            Expiration = expirationString,
                            ExpirationDateTime = optionExpiration,
                        };
                        heatmapRows.Add(spreadHeatmapRow);
                        _expirationToSpreadHeatmapRowMap[expirationString] = spreadHeatmapRow;
                    }
                    spreadHeatmapRow.SetCell(_nextColIndex,
                                             optionExpiration,
                                             optionChain,
                                             heatmapSettingsModel,
                                             GlobalHeatmapSettingsModel,
                                             groupAlert,
                                             deltaStore);
                }
            }

            Dispatcher.Invoke(() =>
            {
                foreach (OptionChainModel optionChain in optionChains)
                {
                    OptionChains.Add(optionChain);
                }

                foreach (SpreadHeatmapRowModel model in heatmapRows)
                {
                    HeatmapRows.Add(model);
                }

                foreach (HeatmapSettingsModel model in underlyngSettings)
                {
                    UnderlyngSettings.Add(model);
                }
            });
        }

        private void SetColumns(int nextColIndex, string symbol, bool visible)
        {
            switch (nextColIndex)
            {
                case 1:
                    HeaderCol1 = symbol;
                    HeaderColVisible1 = visible;
                    break;
                case 2:
                    HeaderCol2 = symbol;
                    HeaderColVisible2 = visible;
                    break;
                case 3:
                    HeaderCol3 = symbol;
                    HeaderColVisible3 = visible;
                    break;
                case 4:
                    HeaderCol4 = symbol;
                    HeaderColVisible4 = visible;
                    break;
                case 5:
                    HeaderCol5 = symbol;
                    HeaderColVisible5 = visible;
                    break;
                case 6:
                    HeaderCol6 = symbol;
                    HeaderColVisible6 = visible;
                    break;
                case 7:
                    HeaderCol7 = symbol;
                    HeaderColVisible7 = visible;
                    break;
                case 8:
                    HeaderCol8 = symbol;
                    HeaderColVisible8 = visible;
                    break;
                case 9:
                    HeaderCol9 = symbol;
                    HeaderColVisible9 = visible;
                    break;
                case 10:
                    HeaderCol10 = symbol;
                    HeaderColVisible10 = visible;
                    break;
                case 11:
                    HeaderCol11 = symbol;
                    HeaderColVisible11 = visible;
                    break;
                case 12:
                    HeaderCol12 = symbol;
                    HeaderColVisible12 = visible;
                    break;
                case 13:
                    HeaderCol13 = symbol;
                    HeaderColVisible13 = visible;
                    break;
                case 14:
                    HeaderCol14 = symbol;
                    HeaderColVisible14 = visible;
                    break;
                case 15:
                    HeaderCol15 = symbol;
                    HeaderColVisible15 = visible;
                    break;
                case 16:
                    HeaderCol16 = symbol;
                    HeaderColVisible16 = visible;
                    break;
                case 17:
                    HeaderCol17 = symbol;
                    HeaderColVisible17 = visible;
                    break;
                case 18:
                    HeaderCol18 = symbol;
                    HeaderColVisible18 = visible;
                    break;
                case 19:
                    HeaderCol19 = symbol;
                    HeaderColVisible19 = visible;
                    break;
                case 20:
                    HeaderCol20 = symbol;
                    HeaderColVisible20 = visible;
                    break;
                case 21:
                    HeaderCol21 = symbol;
                    HeaderColVisible21 = visible;
                    break;
                case 22:
                    HeaderCol22 = symbol;
                    HeaderColVisible22 = visible;
                    break;
                case 23:
                    HeaderCol23 = symbol;
                    HeaderColVisible23 = visible;
                    break;
                case 24:
                    HeaderCol24 = symbol;
                    HeaderColVisible24 = visible;
                    break;
                case 25:
                    HeaderCol25 = symbol;
                    HeaderColVisible25 = visible;
                    break;
                case 26:
                    HeaderCol26 = symbol;
                    HeaderColVisible26 = visible;
                    break;
                case 27:
                    HeaderCol27 = symbol;
                    HeaderColVisible27 = visible;
                    break;
                case 28:
                    HeaderCol28 = symbol;
                    HeaderColVisible28 = visible;
                    break;
                case 29:
                    HeaderCol29 = symbol;
                    HeaderColVisible29 = visible;
                    break;
                case 30:
                    HeaderCol30 = symbol;
                    HeaderColVisible30 = visible;
                    break;
                case 31:
                    HeaderCol31 = symbol;
                    HeaderColVisible31 = visible;
                    break;
                case 32:
                    HeaderCol32 = symbol;
                    HeaderColVisible32 = visible;
                    break;
                case 33:
                    HeaderCol33 = symbol;
                    HeaderColVisible33 = visible;
                    break;
                case 34:
                    HeaderCol34 = symbol;
                    HeaderColVisible34 = visible;
                    break;
                case 35:
                    HeaderCol35 = symbol;
                    HeaderColVisible35 = visible;
                    break;
                case 36:
                    HeaderCol36 = symbol;
                    HeaderColVisible36 = visible;
                    break;
                case 37:
                    HeaderCol37 = symbol;
                    HeaderColVisible37 = visible;
                    break;
                case 38:
                    HeaderCol38 = symbol;
                    HeaderColVisible38 = visible;
                    break;
                case 39:
                    HeaderCol39 = symbol;
                    HeaderColVisible39 = visible;
                    break;
                case 40:
                    HeaderCol40 = symbol;
                    HeaderColVisible40 = visible;
                    break;
                case 41:
                    HeaderCol41 = symbol;
                    HeaderColVisible41 = visible;
                    break;
                case 42:
                    HeaderCol42 = symbol;
                    HeaderColVisible42 = visible;
                    break;
                case 43:
                    HeaderCol43 = symbol;
                    HeaderColVisible43 = visible;
                    break;
                case 44:
                    HeaderCol44 = symbol;
                    HeaderColVisible44 = visible;
                    break;
                case 45:
                    HeaderCol45 = symbol;
                    HeaderColVisible45 = visible;
                    break;
                case 46:
                    HeaderCol46 = symbol;
                    HeaderColVisible46 = visible;
                    break;
                case 47:
                    HeaderCol47 = symbol;
                    HeaderColVisible47 = visible;
                    break;
                case 48:
                    HeaderCol48 = symbol;
                    HeaderColVisible48 = visible;
                    break;
                case 49:
                    HeaderCol49 = symbol;
                    HeaderColVisible49 = visible;
                    break;
                case 50:
                    HeaderCol50 = symbol;
                    HeaderColVisible50 = visible;
                    break;
                default:
                    break;
            }
        }

        private void EditAlerts(dynamic parameter)
        {
            try
            {
                if (parameter != null)
                {
                    SpreadHeatmapAlert spreadHeatmapAlert = null;
                    if (parameter is SpreadHeatmapAlert alert)
                    {
                        spreadHeatmapAlert = alert;
                    }
                    else
                    {
                        spreadHeatmapAlert = (SpreadHeatmapAlert)parameter.SpreadHeatmapAlert;
                    }
                    if (spreadHeatmapAlert != null)
                    {
                        EditHeatmapAlertViewModel viewModel = new()
                        {
                            SpreadHeatmapAlert = spreadHeatmapAlert,
                            Enabled = spreadHeatmapAlert.AlertEnabled,
                            Threshold = spreadHeatmapAlert.Threshold,
                            AudioEnabled = spreadHeatmapAlert.AudioEnabled,
                            AudioSound = spreadHeatmapAlert.AudioSound,
                            VisualEnabled = spreadHeatmapAlert.VisualEnabled,
                            NotificationEnabled = spreadHeatmapAlert.NotificationEnabled,
                        };
                        EditHeatmapAlertView view = new()
                        {
                            DataContext = viewModel
                        };

                        Dispatcher?.BeginInvoke(new Action(() => view.Show()));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditAlerts));
            }
        }

        private void ShowinOptionChain(dynamic parameter)
        {
            try
            {
                if (parameter != null)
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        OptionChainView window = new();
                        OptionChainViewModel viewModel = (OptionChainViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) =>
                        {
                            viewModel.Underlying = parameter.Underlying;
                            viewModel.LoadOptionChain().ContinueWith(t =>
                            {
                                window.SelectGroup((DateTime)parameter.Expiration);
                            });
                        };

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShowinOptionChain));
            }
        }

        public void LoadConfigFromJson(string json)
        {
            try
            {
                HeatmapConfig config = JsonConvert.DeserializeObject<HeatmapConfig>(json);
                LoadFromConfig(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJson));
            }
        }

        public void LoadFromConfig(HeatmapConfig config)
        {
            UnderlyingQuery = config.UnderlyingQuery;
            GlobalHeatmapSettingsModel.HeatMapMode = config.HeatMapMode;
            GlobalHeatmapSettingsModel.Enabled = config.EnableGlobalHeatmap;
            GlobalHeatmapSettingsModel.Operator = config.Operator;
            GlobalHeatmapSettingsModel.TotalDays = config.Days;
            GlobalHeatmapSettingsModel.TotalMins = config.Mins;
            GlobalHeatmapSettingsModel.Delta = config.Delta;
            GlobalHeatmapSettingsModel.GlobalAlert.Threshold = config.SpreadHeatmapAlert.Threshold;
            GlobalHeatmapSettingsModel.GlobalAlert.AlertEnabled = config.SpreadHeatmapAlert.AlertEnabled;
            GlobalHeatmapSettingsModel.GlobalAlert.AudioEnabled = config.SpreadHeatmapAlert.AudioEnabled;
            GlobalHeatmapSettingsModel.GlobalAlert.AudioSound = config.SpreadHeatmapAlert.AudioSound;
            GlobalHeatmapSettingsModel.GlobalAlert.Threshold = config.SpreadHeatmapAlert.Threshold;
            GlobalHeatmapSettingsModel.GlobalAlert.VisualEnabled = config.SpreadHeatmapAlert.VisualEnabled;
            GlobalHeatmapSettingsModel.GlobalAlert.NotificationEnabled = config.SpreadHeatmapAlert.NotificationEnabled;
            GlobalHeatmapSettingsModel.GlobalAlert.ShareWithUsers = config.SpreadHeatmapAlert.ShareWithUsers;
            Output = config.Output;
            OrderTallySymbol = config.OrderTallySymbol;
            ChartType = config.ChartType;
            _ = SearchUnderlying();
            _ = InvokeReady();
        }

        public HeatmapConfig GetConfig()
        {
            List<string> underlyings = OptionChains.Select(x => x.Symbol).ToList();
            if (!string.IsNullOrEmpty(UnderlyingQuery))
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
            return new HeatmapConfig
            {
                UnderlyingQuery = string.Join(",", underlyings.Distinct()),
                HeatMapMode = GlobalHeatmapSettingsModel.HeatMapMode,
                EnableGlobalHeatmap = GlobalHeatmapSettingsModel.Enabled,
                Operator = GlobalHeatmapSettingsModel.Operator,
                Days = GlobalHeatmapSettingsModel.TotalDays,
                Mins = GlobalHeatmapSettingsModel.TotalMins,
                Delta = GlobalHeatmapSettingsModel.Delta,
                SpreadHeatmapAlert = GlobalHeatmapSettingsModel.GlobalAlert,
                Output = Output,
                OrderTallySymbol = OrderTallySymbol,
                ChartType = ChartType,
            };
        }

        public string GetConfigJson()
        {
            return JsonConvert.SerializeObject(GetConfig());
        }

        internal new void Dispose()
        {
            base.Dispose();
            foreach (SpreadHeatmapRowModel map in HeatmapRows)
            {
                map.Dispose();
            }
        }

        internal void LoadTicket(DateTime expiration, double strike)
        {
            if (GlobalHeatmapSettingsModel.CallSelected)
            {
                string callOption = OptionsHelper.GetSymbolFromComponents(OrderTallySymbol, expiration, "CALL", strike);
                if (callOption != null)
                {
                    LoadTicket(callOption);
                }
            }
            if (GlobalHeatmapSettingsModel.PutSelected)
            {
                string putOption = OptionsHelper.GetSymbolFromComponents(OrderTallySymbol, expiration, "PUT", strike);
                if (putOption != null)
                {
                    LoadTicket(putOption);
                }
            }
        }

        private void LoadTicket(string symbol)
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;
                    if (OmsCore.Config.UseOrderTicketForSingleLegOrders)
                    {
                        window = new OrderTicketView();
                    }
                    else
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView();
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView();
                                break;
                        }
                    }

                    ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                    window.Loaded += (s, e) => _ = viewModel.LoadLegsFromTosAsync(symbol, ZeroPlus.Models.Data.Enums.Side.Buy, loadOptions: true);

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return GetConfigJson();
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            await Task.Run(() => LoadConfigFromJson(configJson));
        }
    }
}
