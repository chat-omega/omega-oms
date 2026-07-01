using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Services
{
    public interface ILoadCustomColumnService
    {
        void AddCustomColumn(CustomColumnTemplateModel colTemplate);
    }
}