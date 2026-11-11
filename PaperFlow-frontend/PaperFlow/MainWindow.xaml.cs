using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;


namespace PaperFlow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SendButton_click(object sender, RoutedEventArgs e)
        {
            

        }


        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private static void changetheme(string theme)
        {
            var palette = new ResourceDictionary
            {
                Source = new Uri(
            $"pack://application:,,,/Themes/{theme}.xaml",
            UriKind.Absolute)
            };

            Application.Current.Resources.MergedDictionaries[0] = palette;
        }

        private void NightThemeButton_Checked(object sender, RoutedEventArgs e)
        {
           changetheme("Night");
        }

        private void NightThemeButton_Unchecked(object sender, RoutedEventArgs e)
        {
            changetheme("Day");
        }
    }
}