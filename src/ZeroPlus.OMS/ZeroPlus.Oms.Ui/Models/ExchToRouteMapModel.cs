namespace ZeroPlus.Oms.Ui.Models
{
    public class ExchToRouteMapModel
    {
        private string _exchange;
        private string _route;

        public string Exchange
        {
            get => _exchange;
            set => _exchange = value?.ToUpper();
        }

        public string Route
        {
            get => _route;
            set => _route = value?.ToUpper();
        }
    }
}