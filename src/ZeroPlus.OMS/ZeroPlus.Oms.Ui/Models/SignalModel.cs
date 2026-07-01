using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Editors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.LowLatency.Ext;
using ZeroPlus.Oms.Ui.ViewModels;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.MsgRequests;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class SignalModel : CustomizableTableViewModelBase, IDynamicConfigModel
    {
        [JsonIgnore]
        public IEnumerable<CallPut> CallPuts { get; } = ((CallPut[])Enum.GetValues(typeof(CallPut))).ToList();
        [JsonIgnore]
        public IEnumerable<LoLaSignalTtl> SignalTtls { get; } = ((LoLaSignalTtl[])Enum.GetValues(typeof(LoLaSignalTtl))).ToList();
        [JsonIgnore]
        [Bindable]
        public partial string Title { get; set; }
        [JsonIgnore]
        [Bindable]
        public partial int Id { get; set; }
        [JsonIgnore]
        [Bindable]
        public partial string Creator { get; set; }
        [JsonIgnore]
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }
        [JsonIgnore]
        [Bindable]
        public partial ConfigSave Details { get; set; }
        [JsonProperty]
        [Bindable]
        public partial FastObservableCollection<SignalEntryModel> Signals { get; set; }
        [JsonProperty]
        [Bindable]
        public partial LoLaSignalTtl SignalTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial DateTime ManualSignalTtl { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int LoopOnProfit { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int StaleTradeMs { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int StaleTheoMs { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int MaxSignals { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool ExcludeOnLoss { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool ExcludeOnScratch { get; set; }
        [JsonProperty]
        [Bindable]
        public partial double EdgeMultiplier { get; set; }
        [JsonProperty]
        [Bindable]
        public partial SignalType Type { get; set; }

        public SignalModel()
        {
            ManualSignalTtl = DateTime.Today + TimeSpan.FromHours(16) + TimeSpan.FromMinutes(14);
            Signals = new FastObservableCollection<SignalEntryModel>();
            for (int i = 0; i < 7; i++)
            {
                string tag = ((char)(i + 65)).ToString();
                Signals.Add(new SignalEntryModel(tag));
            }
        }

        [Command]
        public void InitialOrderQtyChangingCommand(EditValueChangingEventArgs eventArgs)
        {
            if (eventArgs.OldValue is int oldVal && eventArgs.NewValue is int newVal && oldVal < newVal)
            {
                if (MessageBoxService.ShowMessage("Are you sure you want size up?", "", MessageButton.YesNoCancel) !=
                    MessageResult.Yes)
                {
                    eventArgs.IsCancel = true;
                }
            }
        }

        public void Save()
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(SignalTtl)] = SignalTtl.ToString(),
                [nameof(ManualSignalTtl)] = ManualSignalTtl.ToString("t"),
                [nameof(LoopOnProfit)] = LoopOnProfit.ToString(),
                [nameof(StaleTradeMs)] = StaleTradeMs.ToString(),
                [nameof(StaleTheoMs)] = StaleTheoMs.ToString(),
                [nameof(MaxSignals)] = MaxSignals.ToString(),
                [nameof(ExcludeOnLoss)] = ExcludeOnLoss.ToString(),
                [nameof(ExcludeOnScratch)] = ExcludeOnScratch.ToString(),
                [nameof(Type)] = Type.ToString(),
                [nameof(EdgeMultiplier)] = JsonConvert.SerializeObject(EdgeMultiplier),
                [nameof(Signals)] = JsonConvert.SerializeObject(Signals.ToList()),
            };
            Details ??= new ConfigSave();
            Details.ConfigJson = JsonConvert.SerializeObject(configDictionary);
            Details.SaveTime = DateTime.Now;
        }

        public void Load()
        {
            if (Details != null && !string.IsNullOrWhiteSpace(Details.ConfigJson))
            {
                var configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Details.ConfigJson);
                if (configDictionary != null)
                {
                    if (configDictionary.TryGetValue(nameof(SignalTtl), out var strSignalTtl))
                    {
                        if (Enum.TryParse(strSignalTtl, true, out LoLaSignalTtl valSignalTtl))
                        {
                            SignalTtl = valSignalTtl;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(ManualSignalTtl), out var manualSignalTtl))
                    {
                        if (DateTime.TryParse(manualSignalTtl, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
                        {
                            ManualSignalTtl = parsedDateTime;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(LoopOnProfit), out var strLoopOnProfit))
                    {
                        if (int.TryParse(strLoopOnProfit, out var valLoopOnProfit))
                        {
                            LoopOnProfit = valLoopOnProfit;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(StaleTradeMs), out var strStaleTradeMs))
                    {
                        if (int.TryParse(strStaleTradeMs, out var valStaleTradeMs))
                        {
                            StaleTradeMs = valStaleTradeMs;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(StaleTheoMs), out var strStaleTheoMs))
                    {
                        if (int.TryParse(strStaleTheoMs, out var valStaleTheoMs))
                        {
                            StaleTheoMs = valStaleTheoMs;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(MaxSignals), out var strMaxSignals))
                    {
                        if (int.TryParse(strMaxSignals, out var valMaxSignals))
                        {
                            MaxSignals = valMaxSignals;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(ExcludeOnLoss), out var strExcludeOnLoss))
                    {
                        if (bool.TryParse(strExcludeOnLoss, out var valExcludeOnLoss))
                        {
                            ExcludeOnLoss = valExcludeOnLoss;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(ExcludeOnScratch), out var strExcludeOnScratch))
                    {
                        if (bool.TryParse(strExcludeOnScratch, out var valExcludeOnScratch))
                        {
                            ExcludeOnScratch = valExcludeOnScratch;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(Type), out var strType))
                    {
                        if (Enum.TryParse(strType, true, out SignalType valType))
                        {
                            Type = valType;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(EdgeMultiplier), out var strEdgeMultiplier))
                    {
                        if (double.TryParse(strEdgeMultiplier, out var valEdgeMultiplier))
                        {
                            EdgeMultiplier = valEdgeMultiplier;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(Signals), out var strSignals))
                    {
                        List<SignalEntryModel> signals =
                            JsonConvert.DeserializeObject<List<SignalEntryModel>>(strSignals);
                        if (signals != null && signals.Any())
                        {
                            Signals.Clear();
                            Signals.AddRange(signals);
                        }
                    }
                }
            }
        }

        public jsonRequestSignalController GetParams(string tag = "A")
        {
            jsonRequestSignalController signalController = new jsonRequestSignalController
            {
                TradeWatcher = JsonRequestSignalTradeWatcherParamsEmpty,
            };

            var signalEntryModel = Signals.FirstOrDefault(x => x.Tag == tag);
            var param = signalEntryModel.JsonRequestSignalTradeWatcherInstanceParams(true, ExcludeOnLoss, ExcludeOnScratch);
            switch (signalEntryModel.Tag)
            {
                case "A":
                    signalController.TradeWatcher.InstanceA = param;
                    break;
                case "B":
                    signalController.TradeWatcher.InstanceB = param;
                    break;
                case "C":
                    signalController.TradeWatcher.InstanceC = param;
                    break;
                case "D":
                    signalController.TradeWatcher.InstanceD = param;
                    break;
                case "E":
                    signalController.TradeWatcher.InstanceE = param;
                    break;
                case "F":
                    signalController.TradeWatcher.InstanceF = param;
                    break;
                case "G":
                    signalController.TradeWatcher.InstanceG = param;
                    break;
            }

            return signalController;
        }

        public MsgRequests.jsonRequestSignalTradeWatcherParams JsonRequestSignalTradeWatcherParams(bool partialUpload)
        {
            var param = JsonRequestSignalTradeWatcherParamsEmpty;

            foreach (var instance in Signals)
            {
                var fieldInstanceX = typeof(jsonRequestSignalTradeWatcherParams).GetField($"Instance{instance.Tag}");
                if (fieldInstanceX == null) continue;

                fieldInstanceX.SetValue(param, instance.JsonRequestSignalTradeWatcherInstanceParams(partialUpload, ExcludeOnLoss, ExcludeOnScratch));
            }

            return param;
        }

        public jsonRequestSignalTradeWatcherParams JsonRequestSignalTradeWatcherParamsEmpty
        {
            get
            {
                var param = new jsonRequestSignalTradeWatcherParams
                {
                    TTL = ConvertTtl(),
                    StaleTradeMs = StaleTradeMs,
                    StaleTheoMs = StaleTheoMs,
                    MaxNumSignals = MaxSignals,
                    MaxNumLoopsOnProfit = LoopOnProfit,
                };

                return param;
            }
        }

        private string ConvertTtl()
        {
            switch (SignalTtl)
            {
                case LoLaSignalTtl.Time1614:
                    return "16:14";
                case LoLaSignalTtl.Time1559:
                    return "15:59";
                case LoLaSignalTtl.Min1:
                    return "1 Min";
                case LoLaSignalTtl.Min15:
                    return "15 Min";
                case LoLaSignalTtl.Hour1:
                    return "1 Hour";
                case LoLaSignalTtl.Hour2:
                    return "2 Hour";
                case LoLaSignalTtl.Manual:
                    return ManualSignalTtl.ToString("HH:mm");
                case LoLaSignalTtl.EOD:
                    return IsHalfDay() ? "13:00" : "15:59";
                case LoLaSignalTtl.EOD_ETH:
                    return IsHalfDay() ? "13:00" : "16:14";
                case LoLaSignalTtl.Blank:
                default:
                    return "";
            }
        }

        private bool IsHalfDay()
        {
            return OmsCore.Config.HalfDays.Contains(DateTime.Today.ToString("yyyy-MM-dd"));
        }
    }
}
