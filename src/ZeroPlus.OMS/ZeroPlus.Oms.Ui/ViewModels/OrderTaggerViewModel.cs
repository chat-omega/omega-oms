using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using ZeroPlus.Hercules.Client.Interfaces;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class OrderTaggerViewModel : ViewModelBase
    {
        private readonly IHerculesClient _herculesClient;

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial string OrderId { get; set; }

        [Bindable]
        public partial string SpreadId { get; set; }

        [Bindable]
        public partial string Tagger { get; set; }

        [Bindable]
        public partial string Message { get; set; }

        public OrderTaggerViewModel(IHerculesClient herculesClient)
        {
            _herculesClient = herculesClient;
        }

        [Command]
        public void SendTagCommand()
        {
            _herculesClient.TagOrder(OrderId, true, Tagger, Message);
            CurrentWindowService?.Close();
        }

        [Command]
        public void CancelCommand()
        {
            CurrentWindowService?.Close();
        }
    }
}
