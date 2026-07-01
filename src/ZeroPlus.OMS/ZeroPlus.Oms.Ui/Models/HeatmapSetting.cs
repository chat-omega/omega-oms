using DevExpress.Mvvm;
using System.Linq;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void HeatmapRangeChangedEventHandler(bool runQuery = false);
    public partial class HeatmapSettingsModel : BindableBase
    {
        public event HeatmapRangeChangedEventHandler HeatmapRangeChangedEvent;

        private readonly object _rankLock = new();

        public bool _Enabled;
        public HeatMapMode _HeatMapMode;
        public Operator _Operator;
        public int _TotalDays;
        public int _TotalMins;
        public double _Delta;
        public bool _CallSelected;
        public bool _PutSelected;
        public FastObservableCollection<SpreadHeatmapCell> _TopValues;

        public double Min { get; private set; } = double.MaxValue;
        public double Max { get; private set; } = double.MinValue;

        public bool Enabled
        {
            get => _Enabled;
            set
            {
                SetValue(ref _Enabled, value);
                HeatmapRangeChangedEvent?.Invoke();
            }
        }

        [Bindable]
        public partial HeatMapMode HeatMapMode { get; set; }

        [Bindable]
        public partial Operator Operator { get; set; }

        [Bindable]
        public partial int TotalDays { get; set; }

        [Bindable]
        public partial int TotalMins { get; set; }

        [Bindable]
        public partial double Delta { get; set; }

        [Bindable]
        public partial bool CallSelected { get; set; }

        [Bindable]
        public partial bool PutSelected { get; set; }

        [Bindable]
        public partial FastObservableCollection<SpreadHeatmapCell> TopValues { get; set; }

        public bool HistoricLoadEnabled => TotalDays > 0 || TotalMins > 0;

        public SpreadHeatmapAlert GlobalAlert { get; set; }

        public HeatmapSettingsModel()
        {
            TopValues = new FastObservableCollection<SpreadHeatmapCell>();
            GlobalAlert = new SpreadHeatmapAlert();
            CallSelected = true;
            PutSelected = true;
        }

        public HeatmapSettingsModel(int topCount, NotificationManager notificationManager) : this()
        {
            for (int i = 0; i < topCount; i++)
            {
                TopValues.AddItem(new SpreadHeatmapCell(notificationManager));
            }
        }

        internal void SetValue(double spread)
        {
            SetMin(spread);
            SetMax(spread);
        }

        internal void SetMin(double value)
        {
            if (value < Min)
            {
                Min = value;
                HeatmapRangeChangedEvent?.Invoke();
            }
        }

        internal void SetMax(double value)
        {
            if (value > Max)
            {
                Max = value;
                HeatmapRangeChangedEvent?.Invoke();
            }
        }

        internal void Reset()
        {
            Min = double.MaxValue;
            Max = double.MinValue;
        }

        internal void Reset(bool forceRefresh)
        {
            Reset();
            HeatmapRangeChangedEvent?.Invoke(forceRefresh);
            if (forceRefresh)
            {
                foreach (SpreadHeatmapCell cell in TopValues)
                {
                    cell.Reset();
                }
            }
        }

        internal void ClearFromTop(string symbol)
        {
            lock (_rankLock)
            {
                for (int i = 0; i < TopValues.Count; i++)
                {
                    SpreadHeatmapCell cell = TopValues[i];
                    if (cell.Symbol == symbol)
                    {
                        cell.Reset();
                        cell.Symbol = "";
                        break;
                    }
                }
            }
        }

        internal void CompareForTop(string symbol, double value)
        {
            lock (_rankLock)
            {
                for (int i = 0; i < TopValues.Count; i++)
                {
                    SpreadHeatmapCell cell = TopValues[i];
                    if (cell.Symbol == symbol)
                    {
                        cell.Spread = value;
                        break;
                    }
                    else if (string.IsNullOrEmpty(cell.Symbol))
                    {
                        cell.Symbol = symbol;
                        cell.Spread = value;
                        break;
                    }
                    else if (i == TopValues.Count - 1)
                    {
                        if (cell.Spread < value)
                        {
                            cell.Symbol = symbol;
                            cell.Spread = value;
                            break;
                        }
                    }
                }
            }
        }

        internal void RemoveGroupFromTop(string symbolToRemove)
        {
            lock (_rankLock)
            {
                for (int i = 0; i < TopValues.Count; i++)
                {
                    SpreadHeatmapCell cell = TopValues[i];
                    if (cell.Symbol != null && cell.Symbol.Length > 0 && cell.Symbol.Split(" ")[0] == symbolToRemove)
                    {
                        cell.Reset();
                        cell.Symbol = "";
                    }
                }
                IOrderedEnumerable<SpreadHeatmapCell> ordered = TopValues.OrderByDescending(x => x.Spread);
                TopValues = new FastObservableCollection<SpreadHeatmapCell>();
                TopValues.AddRange(ordered.ToList());
            }
        }
    }
}
