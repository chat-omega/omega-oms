using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Indicators
{
    public delegate void EmaUpdatedHandler(double ema);
    public class EmaCalculator
    {
        public event EmaUpdatedHandler EmaUpdatedEvent;

        private bool _running;
        private double _lastUpdate = double.MinValue;
        public readonly IEmaConfig EmaConfig;

        public SubscriptionFieldType QuoteType { get; }
        public double Ema { get; private set; }

        public EmaCalculator(IEmaConfig basketSettings, SubscriptionFieldType quoteType)
        {
            Ema = double.NaN;
            EmaConfig = basketSettings;
            QuoteType = quoteType;
            ResetLastUpdate();
        }

        public void Reset()
        {
            if (EmaConfig == null)
            {
                return;
            }

            ResetLastUpdate();

            if (_running)
            {
                _running = false;
                Ema = double.NaN;
                UpdateEma();
            }
        }

        public void AddUpdate(double newValue)
        {
            if (EmaConfig == null)
            {
                return;
            }

            if ((QuoteType == SubscriptionFieldType.BidIvEma && newValue > _lastUpdate) ||
                (QuoteType == SubscriptionFieldType.AskIvEma && newValue < _lastUpdate) ||
                (QuoteType == SubscriptionFieldType.BidEma && newValue > _lastUpdate) ||
                (QuoteType == SubscriptionFieldType.AskEma && newValue < _lastUpdate))
            {
                _lastUpdate = newValue;
                if (double.IsNaN(Ema))
                {
                    Ema = _lastUpdate;
                    UpdateEma();
                }
            }
            else if (QuoteType is not SubscriptionFieldType.BidIvEma and
                     not SubscriptionFieldType.AskIvEma and
                     not SubscriptionFieldType.BidEma and
                     not SubscriptionFieldType.AskEma)
            {
                _lastUpdate = newValue;
                if (double.IsNaN(Ema))
                {
                    Ema = _lastUpdate;
                    UpdateEma();
                }
            }

            StartEma();
        }

        public void Prime(double midEma)
        {
            _lastUpdate = midEma;
            Ema = midEma;
        }

        private void StartEma()
        {
            if (!_running)
            {
                _running = true;
                Task.Run(() => RecalculateEmaAsync());
            }
        }

        private void ResetLastUpdate()
        {
            if (QuoteType == SubscriptionFieldType.AskIvEma)
            {
                _lastUpdate = double.MaxValue;
            }
            else if (QuoteType == SubscriptionFieldType.BidIvEma)
            {
                _lastUpdate = double.MinValue;
            }
            else
            {
                _lastUpdate = double.NaN;
            }
        }

        private async Task RecalculateEmaAsync()
        {
            while (_running)
            {
                int emaInterval = (int)EmaConfig.EmaInterval;
                if (emaInterval > 0)
                {
                    await Task.Delay(emaInterval);
                }
                if (_lastUpdate is double.MinValue or
                    double.MaxValue)
                {
                    continue;
                }
                double alpha = EmaConfig.EmaSmoothing / (1 + EmaConfig.EmaPeriods);
                double newEma = (_lastUpdate * alpha) + (Ema * (1 - alpha));
                if (QuoteType == SubscriptionFieldType.AskIvEma)
                {
                    _lastUpdate = double.MaxValue;
                }
                else if (QuoteType == SubscriptionFieldType.BidIvEma)
                {
                    _lastUpdate = double.MinValue;
                }
                Ema = newEma;
                UpdateEma();
            }
        }

        private void UpdateEma()
        {
            EmaUpdatedEvent?.Invoke(Ema);
        }
    }
}
