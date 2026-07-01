namespace ZeroPlus.Models.Data.Update
{
    public class IntervalModel
    {
        public bool Active { get; set; }
        public double MinDelta { get; set; }
        public double MaxDelta { get; set; }
        public double AttemptedEdge { get; set; }
        public double Interval { get; set; }
        public int ResubmitCount { get; set; }
        public string? Route { get; set; }
        public bool DisableRounding { get; set; }
    }
}