using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class StrategyModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private BaseStrategy _strategy;
        private bool _isChecked = true;
        private string _name = string.Empty;

        [Newtonsoft.Json.JsonIgnore]
        public string Name
        {
            get => _name;
            set => SetValue(ref _name, value);
        }

        [Newtonsoft.Json.JsonProperty]
        public bool IsChecked
        {
            get => _isChecked;
            set => SetValue(ref _isChecked, value);
        }

        [Newtonsoft.Json.JsonProperty]
        public BaseStrategy Strategy
        {
            get => _strategy;
            set
            {
                SetValue(ref _strategy, value);
                Name = Utils.OptionStrategy2.ConvertToString(value);
            }
        }

        public StrategyModel()
        {

        }

        public StrategyModel(BaseStrategy strategy)
        {
            Strategy = strategy;
        }

        public override bool Equals(object? obj)
        {
            return obj is StrategyModel other && Strategy == other.Strategy;
        }

        public override int GetHashCode()
        {
            return Strategy.GetHashCode();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}