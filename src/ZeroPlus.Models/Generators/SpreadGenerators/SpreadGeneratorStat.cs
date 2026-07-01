using System.Collections.Generic;

namespace ZeroPlus.Models.Generators.SpreadGenerators
{
    public class SpreadGeneratorStat
    {
        public string? Title { get; set; }
        public HashSet<string> Errors { get; set; } = new HashSet<string>();
        public List<SpreadGeneratorStat> Details { get; set; } = new List<SpreadGeneratorStat>();
        public int Leg1Count { get; set; }
        public int Leg2Count { get; set; }
        public int Leg3Count { get; set; }
        public int Leg4Count { get; set; }
    }
}
