namespace ZeroPlus.Models.Data.Helpers
{
    public class IntRange
    {
        public int Min { get; set; } = int.MinValue;
        public int Max { get; set; } = int.MaxValue;

        public IntRange()
        {
        }

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }
}
