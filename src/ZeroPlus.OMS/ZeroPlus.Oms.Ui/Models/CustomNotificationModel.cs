using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public class CustomNotification
    {
        public string Tag { get; set; }
        public string SubType { get; set; }
        public string Sound { get; set; }
        public bool PartiallsOnly { get; set; }
        public bool CloseOnly { get; set; }
    }

    public partial class CustomNotificationModel : BindableBase
    {
        public string _Tag;
        public string _SubType;
        public string _Sound;
        public bool _PartiallsOnly;
        public bool _CloseOnly;


        [Bindable]
        public partial string Tag { get; set; }

        [Bindable]
        public partial string SubType { get; set; }

        [Bindable]
        public partial string Sound { get; set; }

        [Bindable]
        public partial bool PartiallsOnly { get; set; }

        [Bindable]
        public partial bool CloseOnly { get; set; }

        public CustomNotificationModel()
        {
            Tag = "";
            SubType = "";
            Sound = "";
        }

        public CustomNotification Serialize()
        {
            return new CustomNotification
            {
                Tag = Tag,
                SubType = SubType,
                Sound = Sound,
                PartiallsOnly = PartiallsOnly,
                CloseOnly = CloseOnly,
            };
        }

        public void Derserialize(CustomNotification customNotification)
        {
            Tag = customNotification.Tag;
            SubType = customNotification.SubType;
            Sound = customNotification.Sound;
            PartiallsOnly = customNotification.PartiallsOnly;
            CloseOnly = customNotification.CloseOnly;
        }
    }
}
