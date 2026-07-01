using NLog;
using NuGet.Versioning;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Velopack;
using Velopack.Sources;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Update
{
    public delegate void NewVersionAvailableEventHandler(Information updateInfo);

    public class Updater
    {
        public event NewVersionAvailableEventHandler NewVersionAvailableEvent;

        private const int MIN_INTERVAL = 30000;

        private readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private OmsConfig _config;
        private IUpdateSource _updateLocation;
        private int _updateInterval;
        private CancellationTokenSource _cancellationTokenSource;
        private System.Timers.Timer _updateTimer;
        private SemanticVersion _latestVersion;

        public Updater(OmsConfig config)
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            _latestVersion = new SemanticVersion(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
            LoadConfig(config);
        }

        public void DoUpdateAvailable(Information information) => NewVersionAvailableEvent?.Invoke(information);

        public async Task CheckForUpdateAsync(CancellationToken token = default)
        {
            UpdateManager updateManager = new(_updateLocation);
            if (!updateManager.IsInstalled)
            {
                _log.Trace("Fetching update cancelled, instance not installed correctly");
                return;
            }
            try
            {
                UpdateInfo newVersion = await updateManager.CheckForUpdatesAsync().WaitAsync(token);
                if (newVersion is null || updateManager.CurrentVersion is null) return; // no update available
                await updateManager.DownloadUpdatesAsync(newVersion, cancelToken: token);
                if (token.IsCancellationRequested) return;

                string releaseNotes = string.Join(
                    Environment.NewLine + Environment.NewLine,
                    newVersion.DeltasToTarget.Select(release => release.NotesMarkdown));

                SemanticVersion lastVersion = newVersion.TargetFullRelease.Version;

                if (lastVersion > _latestVersion)
                {
                    _latestVersion = lastVersion;
                    NewVersionAvailableEvent?.Invoke(new Information
                    {
                        Version = lastVersion,
                        UpdateInfo = newVersion,
                        ReleaseNotes = releaseNotes,
                        UpdateManager = updateManager
                    });
                }
            }
            catch (SlimException ex)
            {
                if (_log.IsTraceEnabled)
                {
                    _log.Trace(ex, "{0} -> Failed fetching for updates. Loc: {1}, Interval: {2}", 
                        nameof(CheckForUpdateAsync), 
                        _updateLocation, 
                        _updateInterval);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(CheckForUpdateAsync)} -> Failed fetching for updates. Loc: {_updateLocation}, Interval: {_updateInterval}");
            }
        }



        public string GetCurrentVersion()
        {
            try
            {

                UpdateManager updateManager = new(_updateLocation);
                var appVersion = updateManager.CurrentVersion;
                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                return $"V-{appVersion?.ToString() ?? assemblyVersion?.ToString()}";
            }
            catch (Exception)
            {
                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                return $"V-{assemblyVersion?.ToString()}";
            }
        }

        private void LoadConfig(OmsConfig config)
        {
            if (_config != null)
            {
                _config.ConfigChangedEvent -= OnConfigChangedEvent;
            }

            _config = config;
            _config.ConfigChangedEvent += OnConfigChangedEvent;
            _updateInterval = MIN_INTERVAL;
            _updateLocation = new SimpleWebSource(config.AppUpdateUrl);

            if (_config.CheckForUpdateOnInterval && _cancellationTokenSource == null)
            {
                StartCheckUpdateLoopAsync();
            }
            else if (!_config.CheckForUpdateOnInterval && _cancellationTokenSource != null)
            {
                StopCheckUpdateLoop();
            }
        }

        private void OnConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            LoadConfig(config);
        }

        private void StartCheckUpdateLoopAsync()
        {
            try
            {
                StopCheckUpdateLoop();
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;
                _updateTimer = new System.Timers.Timer(_updateInterval);
                _updateTimer.Elapsed += UpdateTimerElapsed;
                _updateTimer.Start();
            }
            catch (TaskCanceledException)
            {
                _log.Error($"{nameof(StartCheckUpdateLoopAsync)} -> Update loop stopped.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(StartCheckUpdateLoopAsync)} -> Failed to start update loop.");
            }
        }

        private async void UpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await CheckForUpdateAsync(_cancellationTokenSource.Token);
            }
            else
            {
                _updateTimer.Stop();
            }
        }

        private void StopCheckUpdateLoop()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = null;
                _updateTimer?.Stop();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(StopCheckUpdateLoop)} -> Failed to stop update loop.");
            }
        }
    }
}
