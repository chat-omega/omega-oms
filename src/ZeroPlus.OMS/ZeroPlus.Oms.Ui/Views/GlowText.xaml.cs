using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for GlowText.xaml
    /// </summary>
    public partial class GlowText : UserControl
    {
        private static Color _defaultColor = (Color)ColorConverter.ConvertFromString("#47bdfc");

        public static readonly DependencyProperty GlowColorProperty = DependencyProperty.Register("GlowColor", typeof(Color), typeof(GlowText), new PropertyMetadata(_defaultColor));
        public static readonly DependencyProperty ActivateGlowProperty = DependencyProperty.Register("ActivateGlow", typeof(bool), typeof(GlowText), new PropertyMetadata(false));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(GlowText), new PropertyMetadata("Mouse Events"));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Color GlowColor
        {
            get => (Color)GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public bool ActivateGlow
        {
            get => (bool)GetValue(ActivateGlowProperty);
            set => SetValue(ActivateGlowProperty, value);
        }

        public GlowText()
        {
            InitializeComponent();
        }
    }
}