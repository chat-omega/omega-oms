using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LoadDomListViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        [Bindable]
        public partial bool IsBusy { get; set; }

        [Bindable]
        public partial List<DomListInfo> Lists { get; set; }

        [Bindable]
        public partial DomListInfo SelectedList { get; set; }

        public List<DominatorModel> SelectedDominators { get; }
        public List<DominatorTraderModel> Traders { get; }

        public LoadDomListViewModel(List<DominatorModel> selected)
        {
            SelectedDominators = selected;
            LoadDomLists();
        }

        public LoadDomListViewModel(List<DominatorTraderModel> traders)
        {
            Traders = traders;
        }

        [Command]
        public void Load()
        {
            if (SelectedList != null)
            {
                foreach (DominatorModel dominator in SelectedDominators)
                {
                    dominator.LoadList(SelectedList);
                }
                CurrentWindowService?.Close();
            }
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }

        private void LoadDomLists()
        {
            IsBusy = true;
            OmsCore.GatewayClient.GetDomListInfosAsync().ContinueWith(task =>
            {
                List<DomListInfo> lists = task.Result;
                if (lists != null)
                {
                    Lists = lists.OrderByDescending(x => x.DateMade).ToList();
                }
                IsBusy = false;
            });
        }
    }
}
