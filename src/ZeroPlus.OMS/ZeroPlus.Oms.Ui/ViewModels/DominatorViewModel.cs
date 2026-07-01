using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class DominatorViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private string _moduleTitle;
        private FastObservableCollection<DominatorTraderModel> _dominators;

        public Dispatcher Dispather { get; private set; }

        public string ModuleTitle { get => _moduleTitle; set => SetValue(ref _moduleTitle, value); }
        public FastObservableCollection<DominatorTraderModel> Dominators { get => _dominators; set => SetValue(ref _dominators, value); }

        public DominatorViewModel()
        {
            ModuleTitle = "Dominator";
            Dominators = new();
        }

        [Command]
        public void StartCommand(DominatorTraderModel model)
        {
            try
            {
                model?.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopCommand));
            }
        }

        [Command]
        public void StopCommand(DominatorTraderModel model)
        {
            try
            {
                model?.Stop();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopCommand));
            }
        }

        [Command]
        public void RefreshCommand(DominatorTraderModel model)
        {
            try
            {
                model?.Refresh();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RefreshCommand));
            }
        }

        [Command]
        public void DomConfigCommand(DominatorTraderModel model)
        {
            try
            {
                DominatorConfigurationModuleView view = new DominatorConfigurationModuleView();
                if (view.DataContext is DominatorConfigurationModuleViewModel viewModel)
                {
                    viewModel.ConfigViewModel = new DominatorConfigurationViewModel();
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DomConfigCommand));
            }
        }

        [Command]
        public void SizeConfigCommand(DominatorTraderModel model)
        {
            try
            {
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SizeConfigCommand));
            }
        }

        internal void Dispose()
        {
        }

        internal void SetDispatcher(Dispatcher dispatcher)
        {
            Dispather = dispatcher;
        }

        internal void LoadFromSpreadGeneratorResults(List<SpreadGeneratorResults> orders)
        { }
    }
}
