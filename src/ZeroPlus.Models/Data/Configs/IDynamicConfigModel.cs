using System;

namespace ZeroPlus.Models.Data.Configs
{
    public interface IDynamicConfigModel
    {
        int Id { get; set; }
        string Title { get; set; }
        string Creator { get; set; }
        DateTime LastUpdateTime { get; set; }
        ConfigSave? Details { get; set; }

        void Save();
        void Load();
    }
}
