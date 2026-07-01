using System;
using System.Windows;
using Velopack;
using Microsoft.Extensions.Logging;

namespace ZeroPlus.Oms.Ui
{
    public class Program
    {
        private static ILogger _logger;

        [STAThread]
        public static void Main()
        {
            SetupLogger();
            Start();
        }

        private static void SetupLogger()
        {
            try
            {
                using var factory = LoggerFactory.Create(builder => builder.AddConsole());
                _logger = factory.CreateLogger(nameof(Program));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unhandled exception: " + ex);
            }
        }

        private static void Start()
        {
            try
            {
                var bootstrapConfig = StartupHelpers.BootstrapConfig.LoadUIBoostrapConfig();
                VelopackApp.Build()
                    .SetAutoApplyOnStartup(bootstrapConfig?.AutoUpdateOnStart ?? true)
                    .Run();

                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unhandled exception: " + ex);
                _logger?.LogError(ex, nameof(Start));
            }
        }
    }
}
