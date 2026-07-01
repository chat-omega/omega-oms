using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;
using ZeroPlus.Ema.Client.Config.Interfaces;
using ZeroPlus.Hercules.Client.Config;
using ZeroPlus.Hercules.Client.Config.Interfaces;
using ZeroPlus.Interpolator.Client.Config.Interfaces;
using ZeroPlus.Oms.Config;
using ZeroPlus.Raptor.Client.Config;
using ZeroPlus.Raptor.Client.Config.Interfaces;

namespace ZeroPlus.Oms.AddIn.Ribbon.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private string _Message;
        private string _RaptorServerAddress;
        private int _RaptorServerPort;
        private string _InterpolatorServerAddress;
        private int _InterpolatorServerPort;
        private string _EmaServerAddress;
        private int _EmaServerPort;
        private string _AutoTraderServerAddress;
        private int _AutoTraderServerPort;
        private string _TransactionsServerAddress;
        private int _TransactionsServerPort;
        private TransactionSubscriptionMode _transactionSubscriptionMode;
        private ObservableCollection<RaptorClientConfig> _raptorClientConfigs;

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public IEnumerable<TransactionSubscriptionMode> OrderbookSubscriptions { get; } = ((TransactionSubscriptionMode[])Enum.GetValues(typeof(TransactionSubscriptionMode))).ToList();
        public IRaptorClientConfig RaptorClientConfig { get; }
        public IRaptorClientConfigParser RaptorClientConfigParser { get; }
        public IInterpolatorClientConfig InterpolatorClientConfig { get; }
        public IInterpolatorClientConfigParser InterpolatorClientConfigParser { get; }
        public IEmaClientConfig EmaClientConfig { get; }
        public IEmaClientConfigParser EmaClientConfigParser { get; }
        public IAutoTraderClientConfig AutoTraderClientConfig { get; }
        public IAutoTraderClientConfigParser AutoTraderClientConfigParser { get; }
        public IHerculesClientConfig TransactionsClientConfig { get; }
        public IHerculesClientConfigParser TransactionsClientConfigParser { get; }

        public string Message
        {
            get => _Message;
            set => SetValue(ref _Message, value);
        }

        public string ServerAddress
        {
            get => _RaptorServerAddress;
            set => SetValue(ref _RaptorServerAddress, value, SaveRaptorAddress);
        }

        public int ServerPort
        {
            get => _RaptorServerPort;
            set => SetValue(ref _RaptorServerPort, value, SaveRaptorPort);
        }

        public string InterpolatorServerAddress
        {
            get => _InterpolatorServerAddress;
            set => SetValue(ref _InterpolatorServerAddress, value, SaveInterpolatorAddress);
        }

        public int InterpolatorServerPort
        {
            get => _InterpolatorServerPort;
            set => SetValue(ref _InterpolatorServerPort, value, SaveInterpolatorPort);
        }

        public string EmaServerAddress
        {
            get => _EmaServerAddress;
            set => SetValue(ref _EmaServerAddress, value, SaveEmaAddress);
        }

        public int EmaServerPort
        {
            get => _EmaServerPort;
            set => SetValue(ref _EmaServerPort, value, SaveEmaPort);
        }

        public string AutoTraderServerAddress
        {
            get => _AutoTraderServerAddress;
            set => SetValue(ref _AutoTraderServerAddress, value, SaveAutoTraderAddress);
        }

        public int AutoTraderServerPort
        {
            get => _AutoTraderServerPort;
            set => SetValue(ref _AutoTraderServerPort, value, SaveAutoTraderPort);
        }

        public string TransactionsServerAddress
        {
            get => _TransactionsServerAddress;
            set => SetValue(ref _TransactionsServerAddress, value, SaveTransactionsAddress);
        }

        public int TransactionsServerPort
        {
            get => _TransactionsServerPort;
            set => SetValue(ref _TransactionsServerPort, value, SaveTransactionsPort);
        }

        public TransactionSubscriptionMode TransactionSubscriptionMode
        {
            get => _transactionSubscriptionMode;
            set => SetValue(ref _transactionSubscriptionMode, value, SaveTransactionsMode);
        }

        public ObservableCollection<RaptorClientConfig> RaptorClientConfigs
        {
            get => _raptorClientConfigs;
            set => SetValue(ref _raptorClientConfigs, value, SaveRaptorClientConfigs);
        }


        public SettingsViewModel(IRaptorClientConfig raptorClientConfig,
                                 IRaptorClientConfigParser raptorClientConfigParser,
                                 IInterpolatorClientConfig interpolatorClientConfig,
                                 IInterpolatorClientConfigParser interpolatorClientConfigParser,
                                 IEmaClientConfig emaClientConfig,
                                 IEmaClientConfigParser emaClientConfigParser, 
                                 IAutoTraderClientConfig autoTraderClientConfig, 
                                 IAutoTraderClientConfigParser autoTraderClientConfigParser, 
                                 IHerculesClientConfig transactionsClientConfig, 
                                 IHerculesClientConfigParser transactionsClientConfigParser)
        {
            OmsCore.Config.ConfigMessageEvent += ShowMessage;
            OmsCore.Config.ConfigChangedEvent += OnConfigChangedEvent;
            RaptorClientConfig = raptorClientConfig;
            RaptorClientConfigs = OmsCore.Config.RaptorClientConfigs.ToObservableCollection();
            RaptorClientConfigParser = raptorClientConfigParser;
            EmaClientConfig = emaClientConfig;
            EmaClientConfigParser = emaClientConfigParser;
            AutoTraderClientConfig = autoTraderClientConfig;
            AutoTraderClientConfigParser = autoTraderClientConfigParser;
            TransactionsClientConfig = transactionsClientConfig;
            TransactionsClientConfigParser = transactionsClientConfigParser;
            InterpolatorClientConfig = interpolatorClientConfig;
            InterpolatorClientConfigParser = interpolatorClientConfigParser;
            Initialize();
        }

        private void Initialize()
        {
            if (RaptorClientConfig != null)
            {
                ServerAddress = RaptorClientConfig.ServerAddress;
                ServerPort = RaptorClientConfig.ServerPort;
            }
            if (InterpolatorClientConfig != null)
            {
                InterpolatorServerAddress = InterpolatorClientConfig.ServerAddress;
                InterpolatorServerPort = InterpolatorClientConfig.ServerPort;
            }
            if (EmaClientConfig != null)
            {
                EmaServerAddress = EmaClientConfig.ServerAddress;
                EmaServerPort = EmaClientConfig.ServerPort;
            }
            if (AutoTraderClientConfig != null)
            {
                AutoTraderServerAddress = AutoTraderClientConfig.ServerAddress;
                AutoTraderServerPort = AutoTraderClientConfig.ServerPort;
            }
            if (TransactionsClientConfig != null)
            {
                TransactionsServerAddress = TransactionsClientConfig.ServerAddress;
                TransactionsServerPort = TransactionsClientConfig.ServerPort;
                TransactionSubscriptionMode = TransactionsClientConfig.TransactionSubscriptionMode;
            }
        }

        protected override void OnInitializeInRuntime()
        {
            Initialize();
        }

        private void SaveRaptorAddress()
        {
            RaptorClientConfig.ServerAddress = ServerAddress;
            SaveRaptorConfig();
        }

        private void SaveRaptorPort()
        {
            RaptorClientConfig.ServerPort = ServerPort;
            SaveRaptorConfig();
        }

        private void SaveEmaAddress()
        {
            EmaClientConfig.ServerAddress = EmaServerAddress;
            SaveEmaConfig();
        }

        private void SaveEmaPort()
        {
            EmaClientConfig.ServerPort = EmaServerPort;
            SaveEmaConfig();
        }

        private void SaveAutoTraderAddress()
        {
            AutoTraderClientConfig.ServerAddress = AutoTraderServerAddress;
            SaveAutoTraderConfig();
        }

        private void SaveAutoTraderPort()
        {
            AutoTraderClientConfig.ServerPort = AutoTraderServerPort;
            SaveAutoTraderConfig();
        }

        private void SaveTransactionsAddress()
        {
            TransactionsClientConfig.ServerAddress = TransactionsServerAddress;
            SaveTransactionsConfig();
        }

        private void SaveTransactionsPort()
        {
            TransactionsClientConfig.ServerPort = TransactionsServerPort;
            SaveTransactionsConfig();
        }

        private void SaveTransactionsMode()
        {
            TransactionsClientConfig.TransactionSubscriptionMode = TransactionSubscriptionMode;
            SaveTransactionsConfig();
        }

        private void SaveInterpolatorAddress()
        {
            InterpolatorClientConfig.ServerAddress = InterpolatorServerAddress;
            SaveInterpolatorConfig();
        }

        private void SaveInterpolatorPort()
        {
            InterpolatorClientConfig.ServerPort = InterpolatorServerPort;
            SaveInterpolatorConfig();
        }

        private void SaveRaptorConfig()
        {
            try
            {
                string status = RaptorClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), RaptorClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private void SaveRaptorClientConfigs()
        {
            try
            {
                OmsCore.Config.RaptorClientConfigs = RaptorClientConfigs.ToList();
                OmsCore.Config.SaveRaptorClientConfig();
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        [Command]
        public void AddNewRaptorClientConfig()
        {
            RaptorClientConfigs.Insert(0, Raptor.Client.Config.RaptorClientConfig.GetDefaultConfig());
            SaveRaptorClientConfigs();
        }

        [Command]
        public void RemoveRaptorClientConfig(RaptorClientConfig config)
        {
            if (config == null)
                return;
            if (RaptorClientConfigs.Contains(config))
                RaptorClientConfigs.Remove(config);
            SaveRaptorClientConfigs();
        }

        private void SaveInterpolatorConfig()
        {
            try
            {
                string status = InterpolatorClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), InterpolatorClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private void SaveEmaConfig()
        {
            try
            {
                string status = EmaClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), EmaClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private void SaveAutoTraderConfig()
        {
            try
            {
                string status = AutoTraderClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), AutoTraderClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private void SaveTransactionsConfig()
        {
            try
            {
                string status = TransactionsClientConfigParser.SaveConfig(OmsConfig.GetConfigDirectory(), TransactionsClientConfig);
                ShowMessage(status);
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private void ShowMessage(string message)
        {
            Message = message;
            SetupClearMessageTimer();
        }

        private void SetupClearMessageTimer()
        {
            Timer timer = new(5000);
            timer.Elapsed += ClearMessage;
            timer.AutoReset = false;
            timer.Start();
        }

        private void ClearMessage(object sender, ElapsedEventArgs e)
        {
            Message = "";
        }

        private void OnConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            _ = Task.Run(() => OnConfigChangedEventAsync(requiresRestart));
        }

        private void OnConfigChangedEventAsync(bool requiresRestart)
        {
            if (requiresRestart)
            {
                bool ok = false;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ok = MessageBox.Show("The config change you made requires reloading the AddIn.\n"
                                         + "Would you like to reload it now?",
                        "ZeroPlus OMS AddIn",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.Yes) == MessageBoxResult.Yes;
                });

                if (ok)
                {
                    // TODO: Implement reload addin here.
                }
            }
        }

    }
}
