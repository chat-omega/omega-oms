using System.Collections.ObjectModel;

namespace ZeroPlus.Oms.Ui.Models
{
    public interface IOrderTicket
    {
        string Underlying { get; set; }
        ObservableCollection<string> AccountsList { get; set; }
        ObservableCollection<string> RoutesList { get; set; }
    }
}