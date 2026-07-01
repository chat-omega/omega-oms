using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public enum LineMode
    {
        Primary,
        Secondary,
    }

    public class ChartConstantLineModel
    {
        public string Title { get; private set; }
        public DateTime Value { get; private set; }
        public LineMode LineMode { get; private set; }

        public ChartConstantLineModel(string title, DateTime value, LineMode lineMode = LineMode.Primary)
        {
            Title = title;
            Value = value;
            LineMode = lineMode;
        }
    }
}
