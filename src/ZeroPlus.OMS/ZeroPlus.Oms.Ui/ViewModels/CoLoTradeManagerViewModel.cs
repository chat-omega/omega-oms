using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum Initiator
    {
        Hunter,
        Drifter,
    }

    public enum Liquidator
    {
        Chaser,
    }

    public partial class CoLoTradeManagerViewModel : CustomizableTableViewModelBase
    {
        public ObservableCollection<string> _Groups;
        public string _Group;
        public string _Symbol;
        public double _Tick;
        public int _Qty;
        public int _ReenableSec;
        public Side _SelectedSide;
        public Initiator _SelectedInitiator;
        public Liquidator _SelectedLiquidator;
        public HunterModel _InitiatorHunterModel;
        public DrifterControlViewModel _InitiatorDrifterModel;
        public DrifterControlViewModel _LiquidatorDrifterModel;
        public ChaserControlModel _LiquidatorChaserModel;


        public Dispatcher Dispatcher { get; private set; }

        public List<double> Ticks { get; } = new List<double>()
        {
            .01,
            .05,
            .10,
            .25,
            .50,
            1,
        };
        public List<Side> Sides { get; } = ((Side[])Enum.GetValues(typeof(Side))).ToList();
        public List<Initiator> Initiators { get; } = ((Initiator[])Enum.GetValues(typeof(Initiator))).ToList();
        public List<Liquidator> Liquidators { get; } = ((Liquidator[])Enum.GetValues(typeof(Liquidator))).ToList();

        [Bindable]
        public partial ObservableCollection<string> Groups { get; set; }

        [Bindable]
        public partial string Group { get; set; }

        [Bindable]
        public partial string Symbol { get; set; }

        [Bindable]
        public partial double Tick { get; set; }

        [Bindable]
        public partial int Qty { get; set; }

        [Bindable]
        public partial int ReenableSec { get; set; }

        [Bindable]
        public partial Side SelectedSide { get; set; }

        [Bindable]
        public partial Initiator SelectedInitiator { get; set; }

        [Bindable]
        public partial Liquidator SelectedLiquidator { get; set; }

        [Bindable]
        public partial HunterModel InitiatorHunterModel { get; set; }

        [Bindable]
        public partial DrifterControlViewModel InitiatorDrifterModel { get; set; }

        [Bindable]
        public partial DrifterControlViewModel LiquidatorDrifterModel { get; set; }

        [Bindable]
        public partial ChaserControlModel LiquidatorChaserModel { get; set; }

        public CoLoTradeManagerViewModel()
        {
            SelectedSide = ZeroPlus.Models.Data.Enums.Side.Buy;
            Tick = Ticks.FirstOrDefault();
            Groups = new ObservableCollection<string>();
            InitiatorHunterModel = new HunterModel();
            InitiatorDrifterModel = new DrifterControlViewModel();
            LiquidatorDrifterModel = new DrifterControlViewModel();
            LiquidatorChaserModel = new ChaserControlModel();
        }

        internal void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }
    }
}
