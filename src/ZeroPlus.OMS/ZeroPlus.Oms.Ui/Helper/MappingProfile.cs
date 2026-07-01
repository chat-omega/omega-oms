using AutoMapper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Models.Data.Enums;
using System.Linq;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            /*
            // AutoMapper automatically matches properties with the same name
            CreateMap<ModuleConfigs.BasketTraderConfig, Models.AutomationConfigModel>()
            //automationConfig.DynamicEdgeModelId = config.DynamicEdgeConfigId;
            .ForMember(d => d.DynamicEdgeModelId, opt => opt.MapFrom(s => s.DynamicEdgeConfigId));
            
            CreateMap<ModuleConfigs.BasketTraderConfig, Models.BasketSettings>().ReverseMap();
            */

            CreateMap<LoopCloseEdgeType, SelectionType>()
                .ConvertUsing(src => src == LoopCloseEdgeType.Static ? SelectionType.Static : SelectionType.Dynamic);

            CreateMap<LoopIntervalType, SelectionType>()
                .ConvertUsing(src => src == LoopIntervalType.Static ? SelectionType.Static : SelectionType.Dynamic);

            CreateMap<LoopIncrementType, SelectionType>()
                .ConvertUsing(src => src == LoopIncrementType.Static ? SelectionType.Static : SelectionType.Dynamic);

            CreateMap<LoopSizeupType, SelectionType>()
                .ConvertUsing(src => src == LoopSizeupType.Static ? SelectionType.Static :
                                     src == LoopSizeupType.Dynamic ? SelectionType.Dynamic : SelectionType.Off);

            CreateMap<AutomationConfigModel, AutoTraderConfig>(MemberList.None)
            .ForPath(dest => dest.DefaultAutomationConfig.PartialFillPercentage,
                opt => opt.MapFrom(src => src.AutomationRequiredPartialFillPercentage))
            .ForPath(dest => dest.DefaultAutomationConfig.PartialFillResubmit,
                opt => opt.MapFrom(src => src.AutomationPartialResubmitCount))
            .ForPath(dest => dest.DefaultAutomationConfig.FreeLookBackUpIncrement,
                opt => opt.MapFrom(src => src.FreeLookOnAllIncrement))
            .ForPath(dest => dest.DefaultAutomationConfig.FreeLookAfterLastAttempt,
                opt => opt.MapFrom(src => src.LoopFreeLook))
            .ForPath(dest => dest.DefaultAutomationConfig.OpenRoute,
                opt => opt.MapFrom(src => src.LooperOpenRoute))
            .ForPath(dest => dest.DefaultAutomationConfig.CloseRoute,
                opt => opt.MapFrom(src => src.LooperCloseRoute))
            .ForPath(dest => dest.DefaultAutomationConfig.OpenRouteSingleLeg,
                opt => opt.MapFrom(src => src.LooperOpenRouteSingleLeg))
            .ForPath(dest => dest.DefaultAutomationConfig.CloseRouteSingleLeg,
                opt => opt.MapFrom(src => src.LooperCloseRouteSingleLeg))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticCloseEdge,
                opt => opt.MapFrom(src => src.ContraFishEdge))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticCloseInterval,
                opt => opt.MapFrom(src => src.ContraFishInterval))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticCloseIntervalMax,
                opt => opt.MapFrom(src => src.ContraFishIntervalMax))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticIncrement,
                opt => opt.MapFrom(src => src.ContraFishPriceIncrement))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticMinLoopEdge,
                opt => opt.MapFrom(src => src.LoopMinEdge))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticMaxLoss,
                opt => opt.MapFrom(src => src.LoopMaxLoss))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticLoopInterval,
                opt => opt.MapFrom(src => src.LoopInterval))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticLoopIntervalMax,
                opt => opt.MapFrom(src => src.LoopIntervalMax))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticSizeUpLoopCountBeforeSizeup,
                opt => opt.MapFrom(src => src.LoopCountBeforeSizeup))
            .ForPath(dest => dest.DefaultAutomationConfig.StaticSizeUp,
                opt => opt.MapFrom(src => src.LoopSizeupQty))
            .ForPath(dest => dest.DefaultAutomationConfig.LastFillResubmitCount,
                opt => opt.MapFrom(src => src.LoopResubmit))
            .ForPath(dest => dest.DefaultAutomationConfig.MaxNumberOfLoops,
                opt => opt.MapFrom(src => src.MaxLoopCount))
            .ForPath(dest => dest.DefaultAutomationConfig.CloseEdgeType,
                opt => opt.MapFrom(src => src.LoopCloseEdgeType))
            .ForPath(dest => dest.DefaultAutomationConfig.CloseIntervalType,
                opt => opt.MapFrom(src => src.LoopIntervalType))
            .ForPath(dest => dest.DefaultAutomationConfig.IncrementType,
                opt => opt.MapFrom(src => src.LoopIncrementType))
            .ForPath(dest => dest.DefaultAutomationConfig.SizeUpType,
                opt => opt.MapFrom(src => src.LoopSizeupType))
            .ForPath(dest => dest.DefaultAutomationConfig.DynamicIncrement,
                opt => opt.MapFrom(src => src.LoopIncrementConfigModel
                    .DynamicIncrementConfigs.Select(x => x.GetConfig())))
            .ForPath(dest => dest.DefaultAutomationConfig.DynamicCloseEdge,
                opt => opt.MapFrom(src => src.DynamicEdgeModel.GetConfig()))
            .ForPath(dest => dest.DefaultAutomationConfig.DynamicCloseInterval,
                opt => opt.MapFrom(src => src.DynamicIntervalModel.GetConfig()))
            .ForPath(dest => dest.DefaultAutomationConfig.ClosePxCrossOption,
                opt => opt.MapFrom(src => src.AdjustClosingPriceToMarket
                    ? PxCrossOption.SmartAdjust : PxCrossOption.Ignore));

            CreateMap<FishLossConfig, AutoTraderConfig>(MemberList.None);
            CreateMap<AutoCancelConfig, AutoTraderConfig>(MemberList.None);

            CreateMap<IAutoTraderSettings, AutoTraderConfig>(MemberList.None)
            .ForPath(dest => dest.DefaultAutomationConfig.DynamicSizeUp, opt => opt.MapFrom(src =>
                src.AutomationConfig.SizeupConfig != null ? src.AutomationConfig.SizeupConfig.GetConfig() : null));
        }
    }
}