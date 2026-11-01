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

        private async void SendButton_click(object sender, RoutedEventArgs e)
        {
            //get message
            string userText = InputTextBox.Text;

            if (string.IsNullOrWhiteSpace(userText)) return;

            //text into json as python asked
            //text = something
            var payload = new { text = userText };
            string jsonString = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            try
            {
                // send request
                HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:8000/transform", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseBody);
                    string reply = doc.RootElement.GetProperty("result").GetString();

                    ResultTextBox.Text = reply;
                }
                else
                {
                    ResultTextBox.Text = "Error" + response.StatusCode;
                }
            }
            catch(Exception ex)
            {
                ResultTextBox.Text = "Error: " + ex.Message;
            }

        }
    }
}