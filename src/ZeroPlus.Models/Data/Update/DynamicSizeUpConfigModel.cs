using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Update
{
    public class DynamicSizeUpConfigModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Creator { get; set; }
        public DateTime LastUpdateTime { get; set; }

        public List<SizeupConfigModel> SizeUpConfigs { get; set; }

        public DynamicSizeUpConfigModel()
        {
            Title = string.Empty;
            Creator = string.Empty;
            SizeUpConfigs = new List<SizeupConfigModel>();
        }
    }
}