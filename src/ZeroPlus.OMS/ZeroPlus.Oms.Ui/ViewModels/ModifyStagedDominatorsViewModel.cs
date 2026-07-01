using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class TimeToEdgeSettingModel : BindableBase
    {
        [Bindable]
        public partial int Spacing { get; set; }

        [Bindable]
        public partial double EdgeChange { get; set; }
    }

    public partial class DeltaToEdgeSettingModel : BindableBase
    {
        [Bindable]
        public partial double Delta { get; set; }

        [Bindable]
        public partial double EdgeChange { get; set; }
    }

    public partial class ModifyStagedDominatorsViewModel : ViewModelBase
    {
        public delegate void ModifyDomsEventHandler(bool updateEdgeMultiplier,
                                                    double edgeMultiplier,
                                                    bool updateDeltaMax,
                                                    double deltaMax,
                                                    bool updateLoopSize,
                                                    int loopSize,
                                                    bool updateDaysToExpiration,
                                                    int minDaysToExpiration,
                                                    int maxDaysToExpiration,
                                                    List<Tuple<int, double>> timeToEdgeSettings,
                                                    List<Tuple<double, double>> deltaToEdgeSettings);

        public event ModifyDomsEventHandler ModifyDomsEvent;


        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public TimeToEdgeSettingModel TimeToEdgeSettingModel1 { get; set; }
        public TimeToEdgeSettingModel TimeToEdgeSettingModel2 { get; set; }
        public TimeToEdgeSettingModel TimeToEdgeSettingModel3 { get; set; }
        public TimeToEdgeSettingModel TimeToEdgeSettingModel4 { get; set; }
        public TimeToEdgeSettingModel TimeToEdgeSettingModel5 { get; set; }
        public TimeToEdgeSettingModel TimeToEdgeSettingModel6 { get; set; }
        public TimeToEdgeSettingModel TimeToEdgeSettingModel7 { get; set; }
        public List<TimeToEdgeSettingModel> TimeToEdgeSettingModels { get; set; }
        public DeltaToEdgeSettingModel LegDeltaToEdgeSettingModel1 { get; set; }
        public DeltaToEdgeSettingModel LegDeltaToEdgeSettingModel2 { get; set; }
        public DeltaToEdgeSettingModel LegDeltaToEdgeSettingModel3 { get; set; }
        public DeltaToEdgeSettingModel SpreadDeltaToEdgeSettingModel { get; set; }
        public List<DeltaToEdgeSettingModel> DeltaToEdgeSettingModels { get; set; }

        [Bindable]
        public partial bool UpdateEdgeMultiplier { get; set; }

        [Bindable]
        public partial double EdgeMultiplier { get; set; }

        [Bindable]
        public partial bool UpdateDeltaMax { get; set; }

        [Bindable]
        public partial double DeltaMax { get; set; }

        [Bindable]
        public partial bool UpdateLoopSize { get; set; }

        [Bindable]
        public partial int LoopSize { get; set; }

        [Bindable]
        public partial bool UpdateDaysToExpiration { get; set; }

        [Bindable]
        public partial int MinDaysToExpiration { get; set; }

        [Bindable]
        public partial int MaxDaysToExpiration { get; set; }

        public ModifyStagedDominatorsViewModel(List<Dictionary<int, double>> calendarEdge, List<Dictionary<double, double>> deltaEdge)
        {
            TimeToEdgeSettingModel1 = new TimeToEdgeSettingModel();
            TimeToEdgeSettingModel2 = new TimeToEdgeSettingModel();
            TimeToEdgeSettingModel3 = new TimeToEdgeSettingModel();
            TimeToEdgeSettingModel4 = new TimeToEdgeSettingModel();
            TimeToEdgeSettingModel5 = new TimeToEdgeSettingModel();
            TimeToEdgeSettingModel6 = new TimeToEdgeSettingModel();
            TimeToEdgeSettingModel7 = new TimeToEdgeSettingModel();

            LegDeltaToEdgeSettingModel1 = new DeltaToEdgeSettingModel();
            LegDeltaToEdgeSettingModel2 = new DeltaToEdgeSettingModel();
            LegDeltaToEdgeSettingModel3 = new DeltaToEdgeSettingModel();
            SpreadDeltaToEdgeSettingModel = new DeltaToEdgeSettingModel();

            TimeToEdgeSettingModels = new List<TimeToEdgeSettingModel>()
            {
                TimeToEdgeSettingModel1,
                TimeToEdgeSettingModel2,
                TimeToEdgeSettingModel3,
                TimeToEdgeSettingModel4,
                TimeToEdgeSettingModel5,
                TimeToEdgeSettingModel6,
                TimeToEdgeSettingModel7,
            };

            DeltaToEdgeSettingModels = new List<DeltaToEdgeSettingModel>()
            {
                LegDeltaToEdgeSettingModel1,
                LegDeltaToEdgeSettingModel2,
                LegDeltaToEdgeSettingModel3,
                SpreadDeltaToEdgeSettingModel,
            };

            if (calendarEdge.Count > 0)
            {
                bool valid = true;
                Dictionary<int, double> first = calendarEdge.First();

                for (int i = 1; i < calendarEdge.Count; i++)
                {
                    Dictionary<int, double> item = calendarEdge[i];
                    valid = ValueEquals(first, item);
                    if (!valid)
                    {
                        break;
                    }
                }

                if (valid)
                {
                    int counter = 0;
                    foreach (KeyValuePair<int, double> edgeKeyValuePair in first)
                    {
                        counter++;
                        switch (counter)
                        {
                            case 1:
                                TimeToEdgeSettingModel1.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel1.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 2:
                                TimeToEdgeSettingModel2.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel2.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 3:
                                TimeToEdgeSettingModel3.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel3.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 4:
                                TimeToEdgeSettingModel4.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel4.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 5:
                                TimeToEdgeSettingModel5.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel5.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 6:
                                TimeToEdgeSettingModel6.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel6.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 7:
                                TimeToEdgeSettingModel7.Spacing = edgeKeyValuePair.Key;
                                TimeToEdgeSettingModel7.EdgeChange = edgeKeyValuePair.Value;
                                break;
                        }
                    }
                }
            }

            if (deltaEdge.Count > 0)
            {
                bool valid = true;
                Dictionary<double, double> first = deltaEdge.First();

                for (int i = 1; i < deltaEdge.Count; i++)
                {
                    Dictionary<double, double> item = deltaEdge[i];
                    valid = ValueEquals(first, item);
                    if (!valid)
                    {
                        break;
                    }
                }

                if (valid)
                {
                    int counter = 0;
                    foreach (KeyValuePair<double, double> edgeKeyValuePair in first)
                    {
                        counter++;
                        switch (counter)
                        {
                            case 1:
                                LegDeltaToEdgeSettingModel1.Delta = edgeKeyValuePair.Key;
                                LegDeltaToEdgeSettingModel1.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 2:
                                LegDeltaToEdgeSettingModel2.Delta = edgeKeyValuePair.Key;
                                LegDeltaToEdgeSettingModel2.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 3:
                                LegDeltaToEdgeSettingModel3.Delta = edgeKeyValuePair.Key;
                                LegDeltaToEdgeSettingModel3.EdgeChange = edgeKeyValuePair.Value;
                                break;
                            case 4:
                                SpreadDeltaToEdgeSettingModel.Delta = edgeKeyValuePair.Key;
                                SpreadDeltaToEdgeSettingModel.EdgeChange = edgeKeyValuePair.Value;
                                break;
                        }
                    }
                }
            }
        }

        public static bool ValueEquals<TKey, TValue>(IDictionary<TKey, TValue> source, IDictionary<TKey, TValue> toCheck)
        {
            if (ReferenceEquals(source, toCheck))
            {
                return true;
            }

            if (source == null || toCheck == null || source.Count != toCheck.Count)
            {
                return false;
            }

            return !source.Except(toCheck).Any();
        }

        [Command]
        public void Modify()
        {
            ModifyDomsEvent?.Invoke(UpdateEdgeMultiplier,
                                    EdgeMultiplier,
                                    UpdateDeltaMax,
                                    DeltaMax,
                                    UpdateLoopSize,
                                    LoopSize,
                                    UpdateDaysToExpiration,
                                    MinDaysToExpiration,
                                    MaxDaysToExpiration,
                                    TimeToEdgeSettingModels.Select(x => Tuple.Create(x.Spacing, x.EdgeChange)).OrderBy(x => x.Item1).ToList(),
                                    DeltaToEdgeSettingModels.Select(x => Tuple.Create(x.Delta, x.EdgeChange)).ToList());
            CurrentWindowService?.Close();
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }
    }
}
