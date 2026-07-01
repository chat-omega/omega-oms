using DevExpress.Mvvm;
using Newtonsoft.Json;

namespace ZeroPlus.Oms.Ui.Models
{
    public class DeltaEdgeModel : BindableBase
    {
        private bool _active = true;
        private double _delta;
        private double _additionalEdgePerContract;
        private double _addedEdge;

        [JsonProperty]
        public bool Active { get => _active; set => SetValue(ref _active, value); }
        [JsonProperty]
        public double Delta { get => _delta; set => SetValue(ref _delta, value); }
        [JsonProperty]
        public double AdditionalEdgePerContract { get => _additionalEdgePerContract; set => SetValue(ref _additionalEdgePerContract, value); }
        [JsonProperty]
        public double AddedEdge { get => _addedEdge; set => SetValue(ref _addedEdge, value); }

        internal DeltaEdgeModel Clone()
        {
            return new DeltaEdgeModel()
            {
                Active = Active,
                Delta = Delta,
                AdditionalEdgePerContract = AdditionalEdgePerContract,
                AddedEdge = AddedEdge,
            };
        }

        internal ZeroPlus.Models.Data.Update.DeltaEdgeModel GetConfig()
        {
            return new ZeroPlus.Models.Data.Update.DeltaEdgeModel()
            {
                Active = Active,
                Delta = Delta,
                AdditionalEdgePerContract = AdditionalEdgePerContract,
                AddedEdge = AddedEdge,
            };
        }
    }
}
