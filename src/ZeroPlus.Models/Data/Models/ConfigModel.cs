using System;

namespace ZeroPlus.Models.Data.Models
{
    public class ConfigModel
    {
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
