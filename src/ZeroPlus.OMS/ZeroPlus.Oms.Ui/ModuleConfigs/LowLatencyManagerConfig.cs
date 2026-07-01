using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class LowLatencyManagerConfig : ModuleConfigBase
    {
        public List<LowLatencyModelConfig> LowLatencyModelConfigs { get; set; } = new();
        public bool AudioMuted { get; set; }
        public bool DisableOpenTicket { get; set; }
    }
}