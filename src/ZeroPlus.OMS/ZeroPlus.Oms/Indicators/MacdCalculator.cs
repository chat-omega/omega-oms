using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Indicators
{
    /// <summary>
    /// 
    /// </summary>
    /// 
    public delegate void MacdUpdatedHandler(double macd, double signal, double bar);
    public class MacdCalculator
    {
        public event MacdUpdatedHandler MacdUpdated;
        private readonly EmaCalculator _fastEma;
        private readonly EmaCalculator _slowEma;
        private readonly EmaCalculator _signalEma;

        public double FastEma
        {
            get => _fastEma.Ema;
            set => _fastEma.AddUpdate(value);
        }
        public double SlowEma
        {
            get => _slowEma.Ema;
            set => _slowEma.AddUpdate(value);
        }
        public double SignalEma
        {
            get => _signalEma.Ema;
            set => _signalEma.AddUpdate(value);
        }

        public EmaConfig FastEmaConfig { get; }
        public EmaConfig SlowEmaConfig { get; }
        public EmaConfig SignalEmaConfig { get; }
        public double Macd { get; set; }

        public double Bar => Macd - SignalEma;

        public MacdCalculator()
        {
            FastEmaConfig = new();
            SlowEmaConfig = new();
            SignalEmaConfig = new();
            _fastEma = new EmaCalculator(FastEmaConfig, SubscriptionFieldType.Ema);
            _slowEma = new EmaCalculator(SlowEmaConfig, SubscriptionFieldType.Ema);
            _signalEma = new EmaCalculator(SignalEmaConfig, SubscriptionFieldType.Ema);

            _slowEma.EmaUpdatedEvent += HandleUpdate;
        }

        private void HandleUpdate(double ema)
        {
            Macd = FastEma - SlowEma;
            SignalEma = Macd;
            MacdUpdated?.Invoke(Macd, SignalEma, Bar);
        }

        public void AddUpdate(double value)
        {
            FastEma = value;
            SlowEma = value;
        }

        public void Start()
        {
            FastEmaConfig.EmaEnabled = true;
            SlowEmaConfig.EmaEnabled = true;
            SignalEmaConfig.EmaEnabled = true;
        }

        public void Stop()
        {
            FastEmaConfig.EmaEnabled = false;
            SlowEmaConfig.EmaEnabled = false;
            SignalEmaConfig.EmaEnabled = false;
        }

        public void Reset()
        {
            _fastEma.Reset();
            _slowEma.Reset();
            _signalEma.Reset();
        }

        public void Prime(double fastEma, double slowEma, double signal)
        {
            _fastEma.Prime(fastEma);
            _slowEma.Prime(slowEma);
            _signalEma.Prime(signal);
        }
    }
}
