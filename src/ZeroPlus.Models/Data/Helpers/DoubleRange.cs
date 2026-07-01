namespace ZeroPlus.Models.Data.Helpers
{
    public class DoubleRange
    {
        public double Min { get; set; } = double.MinValue;
        public double Max { get; set; } = double.MaxValue;

        public DoubleRange()
        {
        }

        public DoubleRange(double min, double max)
        {
            Min = min;
            Max = max;
        }
    }
}
