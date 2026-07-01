using DevExpress.Xpf.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for BrochureView.xaml
    /// </summary>
    public partial class BrochureView : ThemedWindow
    {
        public static string ParentPath => "Images";
        public static string Path => "brochure.png";

        public static void ShowBrochure()
        {
            DirectoryInfo path = Directory.GetParent(ParentPath);
            if (path != null)
            {
                string fullPath = System.IO.Path.Combine(path.FullName, ParentPath, Path);
                if (File.Exists(fullPath))
                {
                    BrochureView view = new BrochureView();
                    view.Show();
                }
            }
        }

        public BrochureView()
        {
            InitializeComponent();
            Loaded += (_, _) => Load();
        }

        private async void Load()
        {
            DirectoryInfo path = Directory.GetParent(ParentPath);
            if (path != null)
            {
                string fullPath = System.IO.Path.Combine(path.FullName, ParentPath, Path);
                if (File.Exists(fullPath))
                {
                    var bitmapImage = await Generate(fullPath);
                    BrochureImage.Source = bitmapImage;
                    Topmost = true;
                    Activate();
                    File.Delete(fullPath);
                }
            }
        }

        private Task<BitmapImage> Generate(string file)
        {
            return Task.Run(() =>
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.UriSource = new Uri(file);
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            });
        }
    }
}
