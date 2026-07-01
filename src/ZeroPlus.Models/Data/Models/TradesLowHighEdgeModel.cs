using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZeroPlus.Models.Data.Models
{
    public class TradesLowHighEdgeModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _Low;
        public double Low
        {
            get => _Low;
            set => SetValue(ref _Low, value);
        }

        private double _High;
        public double High
        {
            get => _High;
            set => SetValue(ref _High, value);
        }

        private double _Edge;
        public double Edge
        {
            get => _Edge;
            set => SetValue(ref _Edge, value);
        }

        public TradesLowHighEdgeModel()
        {
            Low = double.MaxValue;
            High = double.MinValue;
            Edge = double.NaN;
        }

        public void AddUpdate(double adjustedPrice)
        {
            if (adjustedPrice < Low)
            {
                Low = adjustedPrice;
                Edge = High - Low;
            }
            else if (adjustedPrice > High)
            {
                High = adjustedPrice;
                Edge = High - Low;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetValue<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}