using DevExpress.Mvvm.DataAnnotations;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class BulletinBoardViewModel : ModuleViewModelBase
    {
        private readonly BulletinBroker _broker;


        public override Module Module { get; protected set; } = Module.BulletinBoard;

        [Bindable]
        public partial bool AutoScroll { get; set; }
        [Bindable]
        public partial BulletinMessage LastMessage { get; set; }
        [Bindable]
        public partial ObservableCollection<BulletinMessage> Messages { get; set; }

        public BulletinBoardViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, BulletinBroker broker) : base(configBrowserViewModel, omsCore)
        {
            _broker = broker;
            Messages = new ObservableCollection<BulletinMessage>(_broker.GetAllMessages());
            LastMessage = Messages.LastOrDefault();
            _broker.MessageAddedEvent += AddMessage;
            _broker.MessageRemovedEvent += RemoveMessage;
        }

        [Command]
        public void AddMessage(BulletinMessage message)
        {
            Dispatcher?.BeginInvoke(() =>
            {
                Messages.Add(message);
                if (AutoScroll)
                {
                    LastMessage = message;
                }
            });
        }

        [Command]
        public void RemoveMessage(BulletinMessage message)
        {
            Dispatcher?.BeginInvoke(() => Messages.Remove(message));
        }

        public override void OnDispose()
        {
            base.OnDispose();
            _broker.MessageAddedEvent -= AddMessage;
            _broker.MessageRemovedEvent -= RemoveMessage;
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return default;
        }

        public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            return Task.CompletedTask;
        }
    }
}
