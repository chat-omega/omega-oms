namespace ZeroPlus.Oms.Data
{
    public class AccountConfigModel
    {
        public string Account { get; set; }

        public string DefaultBroker { get; set; }

        public string DefaultRoute { get; set; }
        public string DefaultSingleLegRoute { get; set; }
        public string DefaultRouteSpxRutXsp { get; set; }
        public string DefaultRouteNdx { get; set; }
        public string DefaultHedgeRouteRegular { get; set; }
        public string DefaultCurbSessionRouteRegular { get; set; }
        public string DefaultSweepRouteRegular { get; set; }

        public string DefaultRouteAutoTrader { get; set; }
        public string DefaultSingleLegRouteAutoTrader { get; set; }
        public string DefaultRouteSpxRutXspAutoTrader { get; set; }
        public string DefaultRouteNdxAutoTrader { get; set; }
        public string DefaultHedgeRouteAutoTrader { get; set; }
        public string DefaultCurbSessionRouteAutoTrader { get; set; }
        public string DefaultSweepRouteAutoTrader { get; set; }

        public override string ToString()
        {
            return Account;
        }
    }
}
