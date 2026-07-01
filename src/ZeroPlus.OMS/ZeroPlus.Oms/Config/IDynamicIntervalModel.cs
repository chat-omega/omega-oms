using System;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Config
{
    public interface IDynamicIntervalModel
    {
        ConfigSave Details { get; }
        int Id { get; }
        string Title { get; set; }
        string Creator { get; set; }
        DateTime LastUpdateTime { get; set; }

        DynamicIntervalConfigModel GetConfig();
        bool TryGetInterval(double delta,
                            double attemptedEdge,
                            int size,
                            out double interval,
                            out int resubmitCount,
                            out string route,
                            out bool disableRounding);
    }
}