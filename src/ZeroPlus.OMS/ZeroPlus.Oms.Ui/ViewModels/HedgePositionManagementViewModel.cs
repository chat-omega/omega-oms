using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class HedgePositionManagementViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public OmsCore OmsCore { get; }
        private Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        private ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public UnderlyingPositionModel _UnderlyingPositionModel;
        public HedgePositionModel _SelectedPosition;


        [Bindable]
        public partial UnderlyingPositionModel UnderlyingPositionModel { get; set; }
        [Bindable]
        public partial HedgePositionModel SelectedPosition { get; set; }

        public HedgePositionManagementViewModel(OmsCore omsCore)
        {
            OmsCore = omsCore;
        }

        [Command]
        public async Task AddCommand()
        {
            AddHedgePositionView managementView = new();
            if (managementView.DataContext is AddHedgePositionViewModel positionViewModel)
            {
                positionViewModel.UnderlyingPositionModel = UnderlyingPositionModel;
                await Task.Run(() =>
                {
                    positionViewModel.LoadPositions();
                });
                managementView.ShowDialog();
                if (positionViewModel.SelectedPosition != null)
                {
                    SelectedPosition = positionViewModel.SelectedPosition;
                }
            }
            else
            {
                _log.Error(nameof(AddCommand) + " add position manager load failed.");
            }
        }

        [Command]
        public void SaveCommand()
        {
            try
            {
                if (SelectedPosition != null)
                {
                    SelectedPosition.Position.QtyOffSet = SelectedPosition.NetQty - SelectedPosition.Position.NetQty;
                }
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveCommand));
            }
        }

        [Command]
        public void SelectedPositionChangedCommand()
        {
            try
            {
                if (SelectedPosition != null)
                {
                    SelectedPosition.NetQty = SelectedPosition.Position.ActualQty;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SelectedPositionChangedCommand));
            }
        }
    }
}
