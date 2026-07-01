using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Oms.Data.Models
{
    public class ExecutingBrokerFeeModel
    {
        [JsonProperty]
        public ExecutingBroker ExecutingBroker { get; set; }
        [JsonProperty]
        public string BrokerName { get; set; }
        [JsonProperty]
        public double ExecutionFee { get; set; }
        [JsonProperty]
        public double AlgoExecutionFee { get; set; }
        [JsonProperty]
        public double DefaultExchangeFee { get; set; }
        [JsonProperty]
        public List<string> Routes { get; set; }
        [JsonProperty]
        public List<string> AlgoRoutes { get; set; }
        [JsonIgnore]
        public List<RouteModel> AlgoRouteModels => AlgoRoutes.OrderBy(x => x).Select(x => new RouteModel(x)).ToList();
        [JsonIgnore]
        public List<RouteModel> AllRouteModels => Routes.OrderBy(x => x).Select(x => new RouteModel(x)).ToList();

        public ExecutingBrokerFeeModel()
        {
            AlgoRoutes = new List<string>();
            Routes = new List<string>();
        }

        public bool IsAlgoRoute(string route)
        {
            return !string.IsNullOrWhiteSpace(route) && AlgoRoutes != null && AlgoRoutes.Contains(route.ToUpper());
        }
    }
}
