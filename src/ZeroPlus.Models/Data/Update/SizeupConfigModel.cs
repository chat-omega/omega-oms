using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update
{
    public class SizeupConfigModel
    {
        public bool Enabled { get; set; }
        public double Edge { get; set; }
        public double AdditionalEdgePerContract { get; set; }
        public double MaxAbsDelta { get; set; }
        public double MaxUnderWidth { get; set; }
        public int Size { get; set; }
        public ResubmitSizeOption ResubmitSizeOption { get; set; }
        public int RequiredLoop { get; set; }
        public int ResubmitCount { get; set; }
        public int MatchSignalQtyLimit { get; set; }
    }
}