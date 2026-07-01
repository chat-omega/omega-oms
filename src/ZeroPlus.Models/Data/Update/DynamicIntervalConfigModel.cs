using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Models.Data.Update
{
    public class DynamicIntervalConfigModel
    {
        public int Id { get; set; }
        public double DefaultInterval { get; set; }
        public int DefaultResubmit { get; set; }
        public string Title { get; set; }
        public string Creator { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public List<IntervalModel> IntervalTable { get; set; }

        public DynamicIntervalConfigModel()
        {
            Title = string.Empty;
            Creator = string.Empty;
            IntervalTable = new List<IntervalModel>();
            DefaultInterval = 111;
            DefaultResubmit = 0;
        }

        public bool TryGetInterval(double delta, double attemptedEdge, out double interval, out int resubmitCount, out string? route, out bool disableRounding)
        {
            attemptedEdge = Math.Round(attemptedEdge, 2);
            delta = Math.Abs(delta);
            disableRounding = false;

            if (IntervalTable.Count > 0)
            {
                IntervalModel? model = IntervalTable.Where(x => x.Active &&
                                                     x.MinDelta <= delta &&
                                                     x.MaxDelta >= delta &&
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
    }
}