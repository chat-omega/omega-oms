using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Grid;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Oms.Ui.Collections;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LoopModel : BindableBase, IDynamicConfigModel
    {

        [Bindable]
        public partial int Id { get; set; }
        [Bindable]
        public partial string Title { get; set; }
        [Bindable]
        public partial string Creator { get; set; }
        [Bindable]
        public partial ConfigSave Details { get; set; }
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }

        [JsonProperty]
        [Bindable]
        public partial int LoopDelay { get; set; }
        [JsonProperty]
        [Bindable]
        public partial FastObservableCollection<LoopTableModel> LoopTable { get; set; }

        [Command]
        public void ClearColumns(TableView tableView)
        {
            var grid = tableView?.Grid;
            foreach (GridColumn col in grid.Columns.ToList())
            {
                col.Visible = false;
            }
            tableView.ShowColumnChooser();
        }

        public void Save()
        {
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(LoopDelay)] = LoopDelay.ToString(),
                [nameof(LoopTable)] = JsonConvert.SerializeObject(LoopTable.ToList()),
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
                    if (configDictionary.TryGetValue(nameof(LoopDelay), out var strLoopDelay))
                    {
                        if (int.TryParse(strLoopDelay, out var valLoopDelay))
                        {
                            LoopDelay = valLoopDelay;
                        }
                    }
                    if (configDictionary.TryGetValue(nameof(LoopTable), out var strLoopTable))
                    {
                        List<LoopTableModel> loopTable =
                            JsonConvert.DeserializeObject<List<LoopTableModel>>(strLoopTable);
                        if (loopTable != null && loopTable.Any())
                        {
                            LoopTable.Clear();
                            LoopTable.AddRange(loopTable);
                        }
                    }
                }
            }
        }

        public LoopModel()
        {
            LoopTable = new();
            for (int i = 0; i < 5; i++)
            {
                LoopTable.Add(new());
            }
        }
    }
}
