using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZeroPlus.Models.Generators.SpreadGenerators
{
    [Serializable]
    public class ExpirationInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _percent;
        private int _quota;
        private int _count;
        private int _target;
        private DateTime _minExpiration;
        private string? _expiration;

        public ExpirationInfo(string expiration, DateTime minExpiration, int count)
        {
            Expiration = expiration;
            MinExpiration = minExpiration;
            Count = count;
            Percent = 1.0;
        }

        public string? Expiration
        {
            get => _expiration;
            set
            {
                _expiration = value;
                NotifyPropertyChanged();
            }
        }
        public DateTime MinExpiration
        {
            get => _minExpiration;
            set
            {
                _minExpiration = value;
                NotifyPropertyChanged();
            }
        }
        public int Target
        {
            get => _target;
            set
            {
                _target = value;
                NotifyPropertyChanged();
            }
        }
        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                NotifyPropertyChanged();
            }
        }
        public int Quota
        {
            get => _quota;
            set
            {
                _quota = value;
                NotifyPropertyChanged();
            }
        }
        public double Percent
        {
            get => _percent;
            set
            {
                _percent = value;
                UpdateTarget();
                NotifyPropertyChanged();
            }
        }

        public void UpdateTarget(int min = 0)
        {
            Target = Math.Min(Count, Math.Max(min, (int)(Quota * Percent)));
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
