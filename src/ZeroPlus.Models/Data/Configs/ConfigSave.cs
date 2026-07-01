using System;

namespace ZeroPlus.Models.Data.Configs
{
    public class ConfigSave
    {
        public string FullTitle => $"{Username,-10} - {Title}";

        public int Id { get; set; }

        public int OwnerId { get; set; }

        public string? Username { get; set; }

        public DateTime SaveTime { get; set; }

        public int Module { get; set; }

        public string? ConfigJson { get; set; }

        public string? Title { get; set; }

        public string? Group { get; set; }
    }
}
