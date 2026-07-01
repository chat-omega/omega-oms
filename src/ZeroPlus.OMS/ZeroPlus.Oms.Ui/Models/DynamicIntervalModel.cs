using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DynamicIntervalModel : BindableBase, IDynamicIntervalModel
    {
        public double _DefaultInterval;
        public int _DefaultResubmit;
        public string _Title;
        public string _Creator;
        public DateTime _LastUpdateTime;
        public ObservableCollection<IntervalModel> _IntervalTable;

        [JsonIgnore]
        public ConfigSave Details { get; internal set; }

        [JsonProperty]
        public int Id { get; internal set; }

        [JsonProperty]
        [Bindable]
        public partial double DefaultInterval { get; set; }

        [JsonProperty]
        [Bindable]
        public partial int DefaultResubmit { get; set; }

        [JsonProperty]
        [Bindable]
        public partial string Title { get; set; }

        [JsonProperty]
        [Bindable]
        public partial string Creator { get; set; }

        [JsonProperty]
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }

        [JsonProperty]
        [Bindable]
        public partial ObservableCollection<IntervalModel> IntervalTable { get; set; }

        public DynamicIntervalModel()
        {
            IntervalTable = new ObservableCollection<IntervalModel>();
            DefaultInterval = 111;
            DefaultResubmit = 0;
        }

        public bool TryGetInterval(double delta, double attemptedEdge, int size, out double interval, out int resubmitCount, out string route, out bool disableRounding)
        {
            attemptedEdge = Math.Round(attemptedEdge, 2);
            delta = Math.Abs(delta);
            disableRounding = false;

            if (IntervalTable.Count > 0)
            {
                IntervalModel model = IntervalTable.Where(x => x.Active &&
                                                     x.MinDelta <= delta &&
                                                     x.MaxDelta >= delta &&
                                                     x.MinSize <= size &&
                                                     x.AttemptedEdge > attemptedEdge)
                    .OrderBy(x => x.AttemptedEdge)
                    .FirstOrDefault();
                if (model != null)
                {
                    interval = model.Interval;
                    resubmitCount = model.ResubmitCount;
                    route = model.Route;
                    disableRounding = model.DisableRounding;
                    return true;
                }
            }

            interval = DefaultInterval;
            resubmitCount = DefaultResubmit;
            route = null;
            return true;
        }

        internal void CloneFrom(DynamicIntervalModel model)
        {
            LastUpdateTime = DateTime.Now;
            DefaultInterval = DefaultInterval;
            DefaultResubmit = DefaultResubmit;
            IntervalTable = new ObservableCollection<IntervalModel>();
            Title = model.Title;
            Details = null;

            foreach (IntervalModel filter in model.IntervalTable)
            {
                IntervalModel newFilter = filter.Clone();
                if (newFilter != null)
                {
                    IntervalTable.Add(newFilter);
                }
            }
        }

        internal string GetAsJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public ZeroPlus.Models.Data.Update.DynamicIntervalConfigModel GetConfig()
        {
            return new ZeroPlus.Models.Data.Update.DynamicIntervalConfigModel()
            {
                Id = Id,
                DefaultInterval = DefaultInterval,
                DefaultResubmit = DefaultResubmit,
                Title = Title,
                Creator = Creator,
                LastUpdateTime = LastUpdateTime,
                IntervalTable = IntervalTable.Select(x => x.GetConfig()).ToList(),
            };
        }
    }
}
