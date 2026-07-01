using NLog;
using System;
using System.ComponentModel;
using System.Threading;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Services
{
    /// <summary>
    /// Monitors market hours and automatically switches the quote source between Databento and Tron.
    /// When the user configures Databento as their quote source and enables auto-switching,
    /// this scheduler will use Tron outside of market hours and restore Databento during market hours.
    /// The timer fires exactly at the next market open or close transition rather than polling.
    /// </summary>
    public class QuoteSourceScheduler : IDisposable
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly TimeZoneInfo _easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        private readonly OmsConfig _config;
        private readonly OmsCore _omsCore;
        private readonly Timer _timer;
        private readonly object _switchLock = new();

        private QuoteSource _userConfiguredSource;
        private bool _isAutoSwitched;
        private bool _disposed;

        private TimeSpan MarketOpenTimeEastern => TimeSpan.FromTicks(_config.MarketOpenTimeEasternTicks);
        private TimeSpan MarketCloseTimeEastern => TimeSpan.FromTicks(_config.MarketCloseTimeEasternTicks);
        private static DateTime EasternNow => TimeZoneInfo.ConvertTime(DateTime.UtcNow, _easternTimeZone);

        public QuoteSourceScheduler(OmsConfig config, OmsCore omsCore)
        {
            _config = config;
            _omsCore = omsCore;
            _userConfiguredSource = config.QuoteSource;

            _config.PropertyChanged += OnConfigPropertyChanged;

            _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
            _log.Info($"{nameof(QuoteSourceScheduler)} initialized. UserConfiguredSource={_userConfiguredSource}, AutoSwitch={_config.AutoSwitchQuoteSource}");
        }

        private bool IsMarketHours(DateTime easternNow)
        {
            if (easternNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return false;
            }

            TimeSpan timeOfDay = easternNow.TimeOfDay;
            return timeOfDay >= MarketOpenTimeEastern && timeOfDay < MarketCloseTimeEastern;
        }

        /// <summary>
        /// Computes the next market open or close event in Eastern time and returns the
        /// duration from <paramref name="easternNow"/> until that event.
        /// </summary>
        private TimeSpan GetDelayUntilNextEvent(DateTime easternNow)
        {
            TimeSpan open = MarketOpenTimeEastern;
            TimeSpan close = MarketCloseTimeEastern;
            TimeSpan timeOfDay = easternNow.TimeOfDay;

            DateTime nextEvent;

            if (easternNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                int daysUntilMonday = easternNow.DayOfWeek == DayOfWeek.Saturday ? 2 : 1;
                nextEvent = easternNow.Date.AddDays(daysUntilMonday) + open;
            }
            else if (timeOfDay < open)
            {
                nextEvent = easternNow.Date + open;
            }
            else if (timeOfDay < close)
            {
                nextEvent = easternNow.Date + close;
            }
            else
            {
                int daysUntilNextWeekday = easternNow.DayOfWeek == DayOfWeek.Friday ? 3 : 1;
                nextEvent = easternNow.Date.AddDays(daysUntilNextWeekday) + open;
            }

            TimeSpan delay = nextEvent - easternNow;
            if (delay <= TimeSpan.Zero)
            {
                delay = TimeSpan.FromSeconds(1);
            }

            return delay;
        }

        private void ScheduleNextEvent()
        {
            if (_disposed) return;

            if (!_config.AutoSwitchQuoteSource || _userConfiguredSource != QuoteSource.Databento)
            {
                return;
            }

            DateTime easternNow = EasternNow;
            TimeSpan delay = GetDelayUntilNextEvent(easternNow);

            _timer.Change(delay, Timeout.InfiniteTimeSpan);
            _log.Info($"{nameof(QuoteSourceScheduler)}: Next event scheduled in {delay} (at {easternNow + delay:yyyy-MM-dd HH:mm:ss} ET).");
        }

        private void OnTimerTick(object state)
        {
            try
            {
                Evaluate();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(QuoteSourceScheduler)}.{nameof(OnTimerTick)}");
            }
        }

        /// <summary>
        /// Evaluates the current time against market hours, switches the quote source if needed,
        /// and schedules the timer for the next transition.
        /// </summary>
        internal void Evaluate()
        {
            if (!_config.AutoSwitchQuoteSource || _userConfiguredSource != QuoteSource.Databento)
            {
                if (_isAutoSwitched)
                {
                    RestoreToDatabento();
                }

                return;
            }

            DateTime easternNow = EasternNow;
            bool isMarketOpen = IsMarketHours(easternNow);

            lock (_switchLock)
            {
                if (isMarketOpen && _isAutoSwitched)
                {
                    RestoreToDatabento();
                }
                else if (!isMarketOpen && !_isAutoSwitched)
                {
                    SwitchToTron();
                }
            }

            ScheduleNextEvent();
        }

        private void SwitchToTron()
        {
            _log.Info($"{nameof(QuoteSourceScheduler)}: Switching quote source from Databento to Tron (outside market hours).");
            _isAutoSwitched = true;
            _config.EnsureClientEnabledForQuoteSource(QuoteSource.Tron);
            EnsureTronClientConnected();
            _omsCore.QuoteClient.ActiveQuoteSource = QuoteSource.Tron;
        }

        private void RestoreToDatabento()
        {
            _log.Info($"{nameof(QuoteSourceScheduler)}: Restoring quote source to Databento (market hours active).");
            _isAutoSwitched = false;
            _config.EnsureClientEnabledForQuoteSource(QuoteSource.Databento);
            EnsureDatabentoClientConnected();
            _omsCore.QuoteClient.ActiveQuoteSource = QuoteSource.Databento;
        }

        private void EnsureTronClientConnected()
        {
            if (!_omsCore.QuoteClient.IsConnected)
            {
                _log.Info($"{nameof(QuoteSourceScheduler)}: Tron QuoteClient not connected, starting...");
                _ = _omsCore.QuoteClient.StartAsync();
            }
        }

        private void EnsureDatabentoClientConnected()
        {
            if (_omsCore.DatabentoClient != null && !_omsCore.DatabentoClient.IsConnected)
            {
                _log.Info($"{nameof(QuoteSourceScheduler)}: DatabentoClient not connected, starting...");
                _ = _omsCore.DatabentoClient.StartAsync();
            }
        }

        /// <summary>
        /// Handles config property changes to track when the user manually changes the quote source,
        /// toggles auto-switch, or updates market hours times.
        /// </summary>
        private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OmsConfig.QuoteSource))
            {
                _userConfiguredSource = _config.QuoteSource;
                _isAutoSwitched = false;
                _log.Info($"{nameof(QuoteSourceScheduler)}: User changed quote source to {_userConfiguredSource}.");
                Evaluate();
            }
            else if (e.PropertyName == nameof(OmsConfig.AutoSwitchQuoteSource))
            {
                _log.Info($"{nameof(QuoteSourceScheduler)}: AutoSwitchQuoteSource changed to {_config.AutoSwitchQuoteSource}.");
                Evaluate();
            }
            else if (e.PropertyName is nameof(OmsConfig.MarketOpenTimeEasternTicks) or nameof(OmsConfig.MarketCloseTimeEasternTicks))
            {
                _log.Info($"{nameof(QuoteSourceScheduler)}: Market hours changed. Open={MarketOpenTimeEastern}, Close={MarketCloseTimeEastern}.");
                Evaluate();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _config.PropertyChanged -= OnConfigPropertyChanged;
            _timer?.Dispose();

            if (_isAutoSwitched)
            {
                _omsCore.QuoteClient.ActiveQuoteSource = _userConfiguredSource;
                _log.Info($"{nameof(QuoteSourceScheduler)}: Disposed. Restored quote source to {_userConfiguredSource}.");
            }
        }
    }
}
