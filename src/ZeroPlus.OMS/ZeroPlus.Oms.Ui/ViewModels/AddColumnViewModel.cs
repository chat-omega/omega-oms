using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void AddColumnEventHandler(CustomColumnTemplateModel customColumnTemplateModel);

    public partial class AddColumnViewModel : ViewModelBase
    {

        public event AddColumnEventHandler AddColumnEvent;
        public Dispatcher Dispatcher { get; set; }
        protected Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial CustomColumnTemplateModel ColumnTemplate { get; set; }

        public AddColumnViewModel()
        {
            ColumnTemplate = new CustomColumnTemplateModel();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void AddColumn()
        {
            if (string.IsNullOrEmpty(ColumnTemplate.Header))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService.ShowMessage("Header can not be empty", "Add Column", MessageButton.OK, MessageIcon.Warning)
                ));
                return;
            }
            AddColumnEvent?.Invoke(ColumnTemplate);
            CurrentWindowService.Close();
        }
    }
}