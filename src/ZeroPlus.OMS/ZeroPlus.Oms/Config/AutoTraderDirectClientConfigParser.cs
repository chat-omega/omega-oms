using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ZeroPlus.AutoTrader.Client;
using ZeroPlus.AutoTrader.Client.Config;
using ZeroPlus.AutoTrader.Client.Config.Interfaces;

namespace ZeroPlus.Oms.Config
{
    public class AutoTraderDirectClientConfigParser : IAutoTraderClientConfigParser
    {
        public const string LIB_ID = "ZeroPlus.AutoTrader.Direct.Client";

        public string ConfigBaseDir { get; private set; }
        public string DefaultLibConfigPath => Path.Combine(ConfigBaseDir, LIB_ID + ".json");

        public AutoTraderDirectClientConfigParser()
        {
            ConfigBaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(AutoTraderClient));
        }

        public AutoTraderDirectClientConfigParser(string configBaseDir)
        {
            ConfigBaseDir = configBaseDir;
        }

        public List<string> GetSavedConfigsList()
        {
            List<string> list = new();

            if (!File.Exists(DefaultLibConfigPath))
            {
                AutoTraderClientConfig defaultAutoTraderConfig = AutoTraderClientConfig.GetDefaultConfig();
                defaultAutoTraderConfig.ServerAddress = "orders.chi.corp.zeroplusderivatives.com";
                SaveConfig(defaultAutoTraderConfig);
            }

            list.Add(DefaultLibConfigPath);

            return list;
        }

        public string SaveConfig(IAutoTraderClientConfig libConfig)
        {
            return SaveConfig(ConfigBaseDir, libConfig);
        }

        public string SaveConfig(string configPath, IAutoTraderClientConfig libConfig)
        {
            string fullPath = Path.Combine(configPath, LIB_ID + ".json");
            Dictionary<string, IAutoTraderClientConfig> config = new()
            {
                {nameof(AutoTraderDirectClientConfig), libConfig },
            };
            JsonSerializerOptions options = new() { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(fullPath, json);
            return fullPath;
        }

        public static string SaveDefaultConfig(string configPath)
        {
            string fullPath = Path.Combine(configPath, LIB_ID + ".json");
            IAutoTraderClientConfig libConfig = AutoTraderClientConfig.GetDefaultConfig();
            Dictionary<string, IAutoTraderClientConfig> config = new()
            {
                {nameof(AutoTraderDirectClientConfig), libConfig },
            };
            JsonSerializerOptions options = new() { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(fullPath, json);
            return fullPath;
        }
    }
}
