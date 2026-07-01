using System.Collections.ObjectModel;
using System.Windows.Threading;
using ZeroPlus.Hercules.Client.Interfaces;
using ZeroPlus.Models.Data.Trading;

namespace ZeroPlus.Oms.Ui.Models
{
    public class ExecutionTransactionsContainer
    {
        private Dispatcher _dispatcher;
        private readonly IHerculesClient _client;

        public ObservableCollection<Transaction> ExecutionTransactions { get; } = [];

        public ExecutionTransactionsContainer(IHerculesClient client)
        {
            _client = client;
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _client.ClientConnected += OnHerculesClientConnected;
            _client.ExecutionTransactionEvent += OnExecutionTransactionEvent;
        }

        private void OnHerculesClientConnected()
        {
            ClearTransactions();
        }

        private void OnExecutionTransactionEvent(Transaction transaction)
        {
            _dispatcher.BeginInvoke(() => ExecutionTransactions.Add(transaction));
        }

        private void ClearTransactions()
        {
            _dispatcher.BeginInvoke(() => ExecutionTransactions.Clear());
        }
    }
}
