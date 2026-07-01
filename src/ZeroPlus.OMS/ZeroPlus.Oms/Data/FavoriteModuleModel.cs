using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Data
{
    public class FavoriteModuleModel
    {
        public string Module { get; set; }
        public string Caption { get; set; }
        public int ModuleId { get; set; }
        public ConfigSave ConfigSave { get; set; }
    }
}
