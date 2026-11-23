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
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;


namespace PaperFlow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? selectedDocumentId;
        private static readonly HttpClient client = new HttpClient();
        public MainWindow()
        {
            InitializeComponent();
        }

        //Send button
        private async void SendButton_click(object sender, RoutedEventArgs e)
        {
            string message = InputTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(message))
            {
                InputTextBox.Focus();
                return;
            }

            AddChatMessage("You", message);
            InputTextBox.Clear();
            SendButton.IsEnabled = false;
            AddFileButton.IsEnabled = false;
            SendButton.Content = "Waiting...";

            try
            {
                var pendingMessages = new List<ChatMessage>(conversation)
                {
                    new ChatMessage {role = "user", content = message }
                };


                using var response = await client.PostAsJsonAsync(
                    "http://127.0.0.1:8000/chat",
                    new 
                    {
                        messages = pendingMessages,
                        document_id = selectedDocumentId
                    });

                if (!response.IsSuccessStatusCode)
                {
                    AddChatMessage("Status", $"The backend return HTTP{(int)response.StatusCode}.");
                    return;
                }

                using var body =
                    await response.Content.ReadAsStreamAsync();

                using var json = 
                    await JsonDocument.ParseAsync(body);

                string answer = json.RootElement.GetProperty("reply").GetString()
                    ?? throw new Exception("The backend returned an empty reply.");

                conversation.Add(new ChatMessage
                {
                    role = "user",
                    content = message
                });
                conversation.Add(new ChatMessage
                {
                    role = "assistant",
                    content = answer
                });

                AddChatMessage("PaperFlow", answer);
            }
            catch (Exception exc)
            {
                AddChatMessage("ERROR", exc.Message);
            }
            finally
            {
                SendButton.Content = "Send";
                SendButton.IsEnabled = true;
                AddFileButton.IsEnabled = true;
            }
        }

        private void AddChatMessage(string speaker, string message)
        {
            var text = new TextBlock
            {
                Text = $"{speaker}\n{message}",
                TextWrapping = TextWrapping.Wrap
            };

            var bubble = new Border
            {
                Child = text,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            bubble.SetResourceReference(Border.BackgroundProperty, "PanelColor");
            text.SetResourceReference(TextBlock.ForegroundProperty, "InkColor");

            ChatMessagePanel.Children.Add(bubble);
            ChatScrollViewer.ScrollToEnd();

        }

        public sealed class ChatMessage
        {
            public string role { get; set; } = "";
            public string content { get; set; } = "";
        }

        private readonly List<ChatMessage> conversation = new();

        

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

        private async void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose a document",
                Filter = "Text files (*.txt)|*.txt",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            AddFileButton.IsEnabled = false;
            SendButton.IsEnabled = false;

            try
            {
                using var stream = System.IO.File.OpenRead(dialog.FileName);
                using var form = new MultipartFormDataContent();

                form.Add(
                    new StreamContent(stream),
                    "file",
                    System.IO.Path.GetFileName(dialog.FileName));

                using var response = await client.PostAsync(
                    "http://127.0.0.1:8000/document",
                    form);

                if (!response.IsSuccessStatusCode)
                {
                    string details = await response.Content.ReadAsStringAsync();
                    AddChatMessage("Upload failed", details);
                    return;
                }

                using var body =
                    await response.Content.ReadAsStreamAsync();

                using var json =
                    await JsonDocument.ParseAsync(body);

                selectedDocumentId = json.RootElement.GetProperty("document_id").GetString()
                    ?? throw new Exception("The backend returned an empty document DI.");

                AddChatMessage(
                    "Document uploaded",
                    System.IO.Path.GetFileName(dialog.FileName));

            }
            catch (Exception exc)
            {
                AddChatMessage("Upload Failed", exc.Message);
            }
            finally
            {
                AddFileButton.IsEnabled = true;
                SendButton.IsEnabled = true;
            }
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SendButton.IsEnabled)
            {
                return;
            }

            conversation.Clear();
            // history gone
            ChatMessagePanel.Children.Clear();
            InputTextBox.Clear();
            selectedDocumentId = null;
            InputTextBox.Focus();
        }

        public class DocumentItem
        {
            public string document_id { get; set; } = "";
            public string filename { get; set; } = "";
            public int chunk_count { get; set; }
        }
        private async void DocumentInventoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SendButton.IsEnabled)
            {
                return;
            }
            var documents = await client.GetFromJsonAsync<List<DocumentItem>>(
                "http://127.0.0.1:8000/documents");

            DocumentList.ItemsSource = documents;

            ChatView.Visibility = Visibility.Collapsed;
            InventoryView.Visibility = Visibility.Visible;
            pagetitle.Text = "Document Inventory";
        }

        private void UseDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            if (DocumentList.SelectedItem is not DocumentItem document)
            {
                return;
            }

            selectedDocumentId = document.document_id;

            InventoryView.Visibility = Visibility.Collapsed;
            ChatView.Visibility = Visibility.Visible;
            pagetitle.Text = "Paper Desk";

            AddChatMessage("Document selected", document.filename);
            InputTextBox.Focus();
        }
    }
}