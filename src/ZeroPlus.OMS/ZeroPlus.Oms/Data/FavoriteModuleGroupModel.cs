using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Data
{
    public class FavoriteModuleGroupModel
    {
        public string GroupCaption { get; set; }
        public string Module { get; set; }
        public ObservableCollection<FavoriteModuleModel> FavoriteModules { get; set; }
        public int ModuleId { get; set; }

        public FavoriteModuleGroupModel()
        {
            FavoriteModules = new ObservableCollection<FavoriteModuleModel>();
        }

        internal void AddFavorite(string module, ConfigSave configSave)
        {
            FavoriteModuleModel model = FavoriteModules.FirstOrDefault(x => x.Caption == configSave.Title && x.ConfigSave.Id == configSave.Id);

            if (model != null)
            {
                FavoriteModules.Remove(model);
            }

            model = new FavoriteModuleModel
            {
                Module = module,
                Caption = configSave.Title,
                ModuleId = configSave.Module,
                ConfigSave = configSave,
            };
            FavoriteModules.Add(model);
        }

        internal void RemoveFavorite(FavoriteModuleModel favoriteModuleModel)
        {
            FavoriteModules.Remove(favoriteModuleModel);
        }

        internal bool IsEmpty()
        {
            return FavoriteModules.Count == 0;
        }
    }
}
