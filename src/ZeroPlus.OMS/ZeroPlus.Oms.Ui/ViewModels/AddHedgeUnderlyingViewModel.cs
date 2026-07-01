using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class AddHedgeUnderlyingViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly QuoteClient _quoteClient;
        private string _Underlying;
        private string _HedgeUnderlying;

        private Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        private ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial string Message { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        public string Underlying
        {
            get => _Underlying;
            set
            {
                SetValue(ref _Underlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
                if (ShowFullOptions)
                {
                    HedgeUnderlying = value;
                }
            }
        }
        public string HedgeUnderlying
        {
            get => _HedgeUnderlying;
            set => SetValue(ref _HedgeUnderlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }
        [Bindable]
        public partial double HedgeMultiplier { get; set; }
        [Bindable]
        public partial bool ShowFullOptions { get; set; }

        public AddHedgeUnderlyingViewModel(OmsCore omsCore)
        {
            _quoteClient = omsCore.QuoteClient;
            HedgeMultiplier = 1;
        }

        [Command]
        public void AddCommand()
        {
            SearchUnderlyingCommand();
        }

        [Command]
        public async void SearchUnderlyingCommand()
        {
            try
            {
                IsBusy = true;
                List<Data.Securities.Option> symbols = await _quoteClient.GetSymbolsAsync(Underlying);
                IsBusy = false;
                if (symbols.Count > 0)
                {
                    Symbol = Underlying;
                    CurrentWindowService?.Close();
                }
                else
                {
                    Symbol = "";
                    MessageBoxService.ShowMessage("Symbol not found.");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelCommand));
            }
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
