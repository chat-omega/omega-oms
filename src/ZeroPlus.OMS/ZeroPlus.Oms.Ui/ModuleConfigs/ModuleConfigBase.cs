using Newtonsoft.Json;
using NLog;
using System.Threading.Tasks;

namespace ZeroPlus.Oms.Ui.ModuleConfigs
{
    public class ModuleConfigBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public string WindowSetting { get; set; }

        public Task<string> SerializeAsync()
        {
            try
            {
                return Task.Run(() => JsonConvert.SerializeObject(this));
            }
            catch (System.Exception ex)
            {
                _log.Error(ex, nameof(Deserialize));
                return default;
            }
        }

        public string Serialize()
        {
            try
            {
                return JsonConvert.SerializeObject(this);
            }
            catch (System.Exception ex)
            {
                _log.Error(ex, nameof(Deserialize));
                return default;
            }
        }

        public static Task<T> DeserializeAsync<T>(string json)
        {
            try
            {
                return Task.Run(() => Deserialize<T>(json));
            }
            catch (System.Exception ex)
            {
                _log.Error(ex, nameof(Deserialize));
                return default;
            }
        }

        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (System.Exception ex)
            {
                _log.Error(ex, nameof(Deserialize));
                return default;
            }
        }
    }
}