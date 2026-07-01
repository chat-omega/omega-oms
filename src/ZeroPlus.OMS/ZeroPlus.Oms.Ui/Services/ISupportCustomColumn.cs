using System.Collections.Generic;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Services
{
    public interface ISupportCustomColumn
    {
        void AddColumn(CustomColumnTemplateModel colTemplate);
        List<CustomColumnTemplateModel> GetExpressionEditors();
    }
}