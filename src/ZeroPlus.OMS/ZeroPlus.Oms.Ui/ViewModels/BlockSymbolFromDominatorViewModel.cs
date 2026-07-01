using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum Mode
    {
        Underlying,
        Symbol,
        Expiration
    }

    public partial class DominatorHostModel : ViewModelBase
    {

        [Bindable]
        public partial bool Active { get; set; }
        public string Host { get; set; }
        public List<DominatorToBlockModel> Dominators { get; set; }

        [Command]
        public void ActivateAllCommand()
        {
            Active = true;
            foreach (DominatorToBlockModel dominator in Dominators)
            {
                dominator.Active = true;
            }
        }

        [Command]
        public void DeactivateAllCommand()
        {
            Active = false;
            foreach (DominatorToBlockModel dominator in Dominators)
            {
                dominator.Active = false;
            }
        }
    }

    public partial class DominatorToBlockModel : ViewModelBase
    {

        [Bindable]
        public partial bool Active { get; set; }
        public string Instance { get; set; }
        public Dominator Dominator { get; set; }
    }

    public partial class BlockSymbolFromDominatorViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly DominatorsManagerModel _dominatorsManager;
        public List<Mode> Modes { get; } = Enum.GetValues(typeof(Mode)).Cast<Mode>().ToList();
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public List<DominatorHostModel> Dominators { get; set; }

        [Bindable]
        public partial Mode Mode { get; set; }

        [Bindable]
        public partial string Underlyings { get; set; }

        [Bindable]
        public partial string Symbols { get; set; }

        private Dictionary<string, HashSet<DateTime>> _underToExpirationsMap;

        [Bindable]
        public partial string Expirations { get; set; }

        public BlockSymbolFromDominatorViewModel(DominatorsManagerModel dominatorsManager)
        {
            _dominatorsManager = dominatorsManager;
            Mode = Mode.Symbol;
            Dominators = new List<DominatorHostModel>();

            foreach (IGrouping<string, DominatorModel> kvp in _dominatorsManager.Dominators.GroupBy(x => x.Host))
            {
                DominatorHostModel model = new()
                {
                    Host = kvp.Key,
                    Dominators = new List<DominatorToBlockModel>(),
                };
                foreach (DominatorModel instance in kvp)
                {
                    DominatorToBlockModel instanceModel = new()
                    {
                        Instance = instance.Instance,
                        Dominator = instance.Dominator,
                    };
                    model.Dominators.Add(instanceModel);
                }
                Dominators.Add(model);
            }
        }

        [Command]
        public void ActivateAllCommand()
        {
            foreach (DominatorHostModel hostModel in Dominators)
            {
                hostModel?.ActivateAllCommand();
            }
        }

        [Command]
        public void DeactivateAllCommand()
        {
            foreach (DominatorHostModel hostModel in Dominators)
            {
                hostModel?.DeactivateAllCommand();
            }
        }

        [Command]
        public void SendCommand()
        {
            foreach (DominatorHostModel host in Dominators)
            {
                foreach (DominatorToBlockModel dominator in host.Dominators)
                {
                    if (dominator.Active)
                    {
                        switch (Mode)
                        {
                            case Mode.Underlying:
                                dominator.Dominator.BlockUnderlyings(Underlyings);
                                break;
                            case Mode.Symbol:
                                dominator.Dominator.BlockSymbols(Symbols);
                                break;
                            case Mode.Expiration:
                                dominator.Dominator.BlockExpirations(_underToExpirationsMap);
                                break;
                        }
                    }
                }
            }
            CurrentWindowService?.Close();
        }

        [Command]
        public void CancelCommand()
        {
            CurrentWindowService?.Close();
        }

        internal void Load(List<OmsOrderModel> orders)
        {
            try
            {
                Underlyings = string.Join(", ", orders.Select(x => x.UnderlyingSymbol).Distinct().OrderBy(x => x));
                Symbols = string.Join(", ", orders.Select(x => x.Symbol).Distinct().OrderBy(x => x));
                _underToExpirationsMap = new Dictionary<string, HashSet<DateTime>>();
                foreach (OmsOrderModel order in orders)
                {
                    try
                    {
                        SymbolCodec spread = new(order.Symbol);
                        int legsCount = spread.LegCount;

                        if (!_underToExpirationsMap.TryGetValue(order.UnderlyingSymbol, out HashSet<DateTime> expirations))
                        {
                            expirations = new HashSet<DateTime>();
                            _underToExpirationsMap[order.UnderlyingSymbol] = expirations;
                        }

                        for (int i = 0; i < legsCount; i++)
                        {
                            Instrument leg = spread.GetLeg(i);
                            expirations.Add(leg.expiration.Date);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(Load));
                    }
                }
                List<string> expirationsSummary = new();
                foreach (KeyValuePair<string, HashSet<DateTime>> kvp in _underToExpirationsMap.OrderBy(x => x.Key))
                {
                    expirationsSummary.Add("[" + kvp.Key + "] " + string.Join(",", kvp.Value.Select(x => x.ToString("yy-MMM-dd"))));
                }
                Expirations = string.Join(", ", expirationsSummary);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }
    }
}
