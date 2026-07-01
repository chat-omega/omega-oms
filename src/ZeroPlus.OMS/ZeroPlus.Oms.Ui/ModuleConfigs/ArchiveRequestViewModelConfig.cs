using System;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class ArchiveRequestViewModelConfig
    {
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string ApiUsernames { get; set; }
        public string Tags { get; set; }
        public string Symbols { get; set; }
        public string Underlyings { get; set; }
        public string Format { get; set; }
    }
}
