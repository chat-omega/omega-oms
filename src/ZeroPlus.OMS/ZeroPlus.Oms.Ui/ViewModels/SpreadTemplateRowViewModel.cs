using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class SpreadTemplateRowViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();


        public IEnumerable<BaseStrategy> Strategies { get; } = (BaseStrategy[])Enum.GetValues(typeof(BaseStrategy));
        public IEnumerable<Side> Sides { get; } = (Side[])Enum.GetValues(typeof(Side));

        public SpreadTemplateViewModel ParentViewModel { get; set; }

        [Bindable]
        public partial bool IsValid { get; set; }

        [Bindable]
        public partial BaseStrategy Strategy { get; set; }

        [Bindable]
        public partial Side Side { get; set; }

        [Bindable]
        public partial double Leg1Delta { get; set; }

        [Bindable]
        public partial double Leg2Delta { get; set; }

        [Bindable]
        public partial double Leg3Delta { get; set; }

        [Bindable]
        public partial double Leg4Delta { get; set; }

        [Bindable]
        public partial TemplateExpirationModel Leg1Expiration { get; set; }

        [Bindable]
        public partial TemplateExpirationModel Leg2Expiration { get; set; }

        [Bindable]
        public partial TemplateExpirationModel Leg3Expiration { get; set; }

        [Bindable]
        public partial TemplateExpirationModel Leg4Expiration { get; set; }

        [Bindable]
        public partial double EdgeOverride { get; set; }

        [Bindable]
        public partial bool EdgeOverrideEnabled { get; set; }

        [Bindable]
        public partial bool IsLeg3Visible { get; set; }

        [Bindable]
        public partial bool IsLeg4Visible { get; set; }

        [Bindable]
        public partial bool IsLeg2ExpirationVisible { get; set; }

        [Bindable]
        public partial bool IsLeg3ExpirationVisible { get; set; }

        [Bindable]
        public partial bool IsLeg4ExpirationVisible { get; set; }

        public SpreadTemplateRowViewModel(SpreadTemplateViewModel parentViewModel)
        {
            ParentViewModel = parentViewModel;
        }

        [Command]
        public void ExpirationChangedCommand()
        {
            switch (Strategy)
            {
                case BaseStrategy.CALL_CALENDAR:
                case BaseStrategy.PUT_CALENDAR:
                case BaseStrategy.CALL_DIAGONAL:
                case BaseStrategy.PUT_DIAGONAL:
                case BaseStrategy.CALL_CALENDAR_FLY:
                case BaseStrategy.PUT_CALENDAR_FLY:
                    IsLeg2ExpirationVisible = true;
                    break;
                default:
                    Leg2Expiration = Leg1Expiration;
                    Leg3Expiration = Leg1Expiration;
                    Leg4Expiration = Leg1Expiration;
                    IsLeg2ExpirationVisible = false;
                    break;
            }

            Validate();
        }

        [Command]
        public void StrategyChangedCommand()
        {
            switch (Strategy)
            {
                case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                case BaseStrategy.CALL_CALENDAR_FLY:
                case BaseStrategy.PUT_CALENDAR_FLY:
                case BaseStrategy.CALL_TRIAGONAL:
                case BaseStrategy.PUT_TRIAGONAL:
                case BaseStrategy.IRON_BUTTERFLY:
                case BaseStrategy.CALL_BUTTERFLY:
                case BaseStrategy.PUT_BUTTERFLY:
                    IsLeg3Visible = true;
                    IsLeg4Visible = false;
                    break;
                case BaseStrategy.CALL_CONDOR:
                case BaseStrategy.PUT_CONDOR:
                case BaseStrategy.CALL_1X3X3X1:
                case BaseStrategy.PUT_1X3X3X1:
                case BaseStrategy.STRADDLE:
                case BaseStrategy.STRANGLE:
                case BaseStrategy.CONVERSION:
                case BaseStrategy.REVERSAL:
                case BaseStrategy.IRON_CONDOR:
                case BaseStrategy.CALL_1x3x2:
                case BaseStrategy.PUT_1x3x2:
                case BaseStrategy.CALL_2x3x1:
                case BaseStrategy.PUT_2x3x1:
                    IsLeg3Visible = true;
                    IsLeg4Visible = true;
                    break;
                default:
                    IsLeg3Visible = false;
                    IsLeg4Visible = false;
                    break;
            }

            switch (Strategy)
            {
                case BaseStrategy.CALL_CALENDAR:
                case BaseStrategy.PUT_CALENDAR:
                case BaseStrategy.CALL_DIAGONAL:
                case BaseStrategy.PUT_DIAGONAL:
                    IsLeg2ExpirationVisible = true;
                    break;
                default:
                    Leg2Expiration = Leg1Expiration;
                    IsLeg2ExpirationVisible = false;
                    break;
            }
            Validate();
        }

        private void Validate()
        {
            IsValid = Strategy switch
            {
                BaseStrategy.CALL_VERTICAL or BaseStrategy.PUT_VERTICAL or BaseStrategy.CALL_1X2 or BaseStrategy.PUT_1X2 or BaseStrategy.CALL_1X3 or BaseStrategy.PUT_1X3 or BaseStrategy.CALL_2X3 or BaseStrategy.PUT_2X3 => Leg1Expiration != null && ParentViewModel.Expirations.Contains(Leg1Expiration) && !Leg1Expiration.IsExpired,
                BaseStrategy.CALL_CALENDAR or BaseStrategy.PUT_CALENDAR or BaseStrategy.CALL_DIAGONAL or BaseStrategy.PUT_DIAGONAL or BaseStrategy.CALL_CALENDAR_FLY or BaseStrategy.PUT_CALENDAR_FLY => Leg1Expiration != null && ParentViewModel.Expirations.Contains(Leg1Expiration) && !Leg1Expiration.IsExpired &&
                                              Leg2Expiration != null && ParentViewModel.Expirations.Contains(Leg2Expiration) && !Leg2Expiration.IsExpired &&
                                              Leg1Expiration != Leg2Expiration,
                BaseStrategy.CALL_BUTTERFLY or BaseStrategy.PUT_BUTTERFLY or BaseStrategy.CALL_SKEWED_BUTTERFLY or BaseStrategy.PUT_SKEWED_BUTTERFLY or BaseStrategy.IRON_BUTTERFLY => Leg1Expiration != null && ParentViewModel.Expirations.Contains(Leg1Expiration) && !Leg1Expiration.IsExpired,
                BaseStrategy.CALL_CONDOR or BaseStrategy.PUT_CONDOR or BaseStrategy.CALL_1X3X3X1 or BaseStrategy.PUT_1X3X3X1 or BaseStrategy.IRON_CONDOR => Leg1Expiration != null && ParentViewModel.Expirations.Contains(Leg1Expiration) && !Leg1Expiration.IsExpired,
                _ => false,
            };
        }

        internal void Reverse()
        {
            Side = Side == Side.Buy ? Side.Sell : Side.Buy;
        }

        internal string GetConfigAsJson()
        {
            return JsonConvert.SerializeObject(GetConfig());
        }

        internal async Task LoadConfigFromJsonAsync(string json)
        {
            try
            {
                SpreadTemplateRowConfig config = await Task.Run(() => JsonConvert.DeserializeObject<SpreadTemplateRowConfig>(json));
                LoadConfig(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        internal SpreadTemplateRowConfig GetConfig()
        {
            return new SpreadTemplateRowConfig()
            {
                Strategy = Strategy,
                Side = Side,
                Leg1Delta = Leg1Delta,
                Leg2Delta = Leg2Delta,
                Leg3Delta = Leg3Delta,
                Leg4Delta = Leg4Delta,
                EdgeOverride = EdgeOverride,
                EdgeOverrideEnabled = EdgeOverrideEnabled,
                Leg1Expiration = Leg1Expiration?.Expiration ?? default,
                Leg2Expiration = Leg2Expiration?.Expiration ?? default,
                Leg3Expiration = Leg3Expiration?.Expiration ?? default,
                Leg4Expiration = Leg4Expiration?.Expiration ?? default,
            };
        }

        private void LoadConfig(SpreadTemplateRowConfig config)
        {
            Strategy = config.Strategy;
            Side = config.Side;
            Leg1Delta = config.Leg1Delta;
            Leg2Delta = config.Leg2Delta;
            Leg3Delta = config.Leg3Delta;
            Leg4Delta = config.Leg4Delta;
            EdgeOverride = config.EdgeOverride;
            EdgeOverrideEnabled = config.EdgeOverrideEnabled;
            Leg1Expiration = ParentViewModel.GetExpiration(config.Leg1Expiration);
            Leg2Expiration = ParentViewModel.GetExpiration(config.Leg2Expiration);
            Leg3Expiration = ParentViewModel.GetExpiration(config.Leg3Expiration);
            Leg4Expiration = ParentViewModel.GetExpiration(config.Leg4Expiration);
        }
    }
}
