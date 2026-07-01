using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class AlertConfigurationModel : BindableBase
    {
        private int _intervalCounter = 0;
        private bool _alertFired = false;


        public static NotificationManager NotificationManager;
        public string AlertName { get; set; }

        [Bindable]
        public partial bool Enabled { get; set; }
        [Bindable]
        public partial double SnoozeInterval { get; set; }
        [Bindable]
        public partial double Threshold { get; set; }
        [Bindable]
        public partial bool AudioEnabled { get; set; }
        [Bindable]
        public partial string AudioSound { get; set; }
        [Bindable]
        public partial bool VisualEnabled { get; set; }
        [Bindable]
        public partial bool NotificationEnabled { get; set; }

        public AlertConfigurationModel(string alertName, NotificationManager notificationManager)
        {
            NotificationManager = notificationManager;
            AlertName = alertName?.ToUpper();
            SnoozeInterval = 5000;
        }

        internal void CheckAlert(double value)
        {
            if (Enabled)
            {
                if (value > Threshold)
                {
                    if (_alertFired && _intervalCounter++ < SnoozeInterval)
                    {
                        return;
                    }
                    else
                    {
                        _alertFired = true;
                        _intervalCounter = 0;
                    }

                    if (NotificationEnabled || VisualEnabled)
                    {
                        NotificationManager.AddAlert("ALERT - " + AlertName + "\nThreshold: " + Threshold + ".\nValue: " + value + ".", DateTime.Now, "Dashboard");
                    }
                    if (AudioEnabled)
                    {
                        SoundManager.Play(AudioSound);
                    }
                }
            }
        }

        internal string SerializeToJson()
        {
            Dictionary<string, object> configDictionary = new()
            {
                [nameof(Enabled)] = Enabled,
                [nameof(Threshold)] = Threshold,
                [nameof(AudioEnabled)] = AudioEnabled,
                [nameof(AudioSound)] = AudioSound,
                [nameof(VisualEnabled)] = VisualEnabled,
                [nameof(NotificationEnabled)] = NotificationEnabled,
                [nameof(SnoozeInterval)] = SnoozeInterval,
            };
            string configJson = JsonConvert.SerializeObject(configDictionary);
            return configJson;
        }

        internal async Task LoadFromJsonAsync(string alertsJson)
        {
            try
            {
                Dictionary<string, object> configDictionary = await Task.Run(() => JsonConvert.DeserializeObject<Dictionary<string, object>>(alertsJson));
                Enabled = (bool)configDictionary[nameof(Enabled)];
                Threshold = (double)configDictionary[nameof(Threshold)];
                AudioEnabled = (bool)configDictionary[nameof(AudioEnabled)];
                AudioSound = (string)configDictionary[nameof(AudioSound)];
                VisualEnabled = (bool)configDictionary[nameof(VisualEnabled)];
                NotificationEnabled = (bool)configDictionary[nameof(NotificationEnabled)];
                SnoozeInterval = (double)configDictionary[nameof(SnoozeInterval)];
            }
            catch (Exception)
            {
            }
        }
    }
}
