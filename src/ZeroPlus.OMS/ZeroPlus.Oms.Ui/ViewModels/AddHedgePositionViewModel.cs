using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using NLog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class AddHedgePositionViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public bool _IsBusy;
        public bool _FromPortfolio;
        public string _Message;
        public ObservableCollection<string> _Symbols;
        public string _Symbol;
        public string _Underlying;
        public OmsPosition _Position;
        public ObservableCollection<OmsPosition> _Positions;
        public UnderlyingPositionModel _UnderlyingPositionModel;
        public HedgePositionModel _SelectedPosition;

        public OmsCore OmsCore { get; }
        private ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public AddHedgePositionViewModel(OmsCore omsCore)
        {
            OmsCore = omsCore;
        }

        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial bool FromPortfolio { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial ObservableCollection<string> Symbols { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string Underlying { get; set; }
        [Bindable]
        public partial OmsPosition Position { get; set; }
        [Bindable]
        public partial ObservableCollection<OmsPosition> Positions { get; set; }
        [Bindable]
        public partial UnderlyingPositionModel UnderlyingPositionModel { get; set; }
        [Bindable]
        public partial HedgePositionModel SelectedPosition { get; set; }

        internal async void LoadPositions()
        {
            try
            {
                IsBusy = true;
                ObservableCollection<string> symbols = (await OmsCore.QuoteClient.GetSymbols(UnderlyingPositionModel.Symbol)).Select(x => x.OptionSymbol).ToObservableCollection();
                symbols.Add(UnderlyingPositionModel.Symbol);
                ObservableCollection<OmsPosition> positions = OmsCore.OrderClient.GetAllPositions(UnderlyingPositionModel.Symbol)?.ToObservableCollection();
                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    Symbols = symbols;
                    Positions = positions;
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        [Command]
        public void AddCommand()
        {
            if (FromPortfolio)
            {
                if (Position != null)
                {
                    if (!UnderlyingPositionModel._symbolToPositionModelMap.ContainsKey(Position.Symbol))
                    {
                        Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(Position.Symbol);
                        SelectedPosition = new HedgePositionModel(Position, option, UnderlyingPositionModel.DeltaHedgeManagerModel);
                        UnderlyingPositionModel.AddPosition(SelectedPosition, overrideExisting: false);
                    }
                }
            }
            else
            {
                if (!UnderlyingPositionModel._symbolToPositionModelMap.ContainsKey(Symbol))
                {
                    Data.Securities.Option option = OptionsHelper.GetOptionFromSymbol(Symbol);
                    SelectedPosition = new HedgePositionModel(option, UnderlyingPositionModel.DeltaHedgeManagerModel);
                    UnderlyingPositionModel.AddPosition(SelectedPosition, overrideExisting: false);
                }
            }
            CurrentWindowService?.Close();
        }

        [Command]
        public void CancelCommand()
        {
            try
            {
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelCommand));
            }
        }
    }
}
