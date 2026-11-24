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
using System.IO;
using System.Linq;
using System.IO.Packaging;
using Path = System.IO.Path;


namespace PaperFlow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> selectedDocumentIds = new();
        private static readonly HttpClient client = new HttpClient();
        public MainWindow()
        {
            InitializeComponent();

            // read histoy
            if (File.Exists(historyPath))
            {
                string json = File.ReadAllText(historyPath);

                savedChats = JsonSerializer.Deserialize<List<SavedChat>>(json)
                    ?? new List<SavedChat>();
            }

            ChatHistoryList.ItemsSource = savedChats;
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
            ChatHistoryList.IsEnabled = false;
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
                        document_ids = selectedDocumentIds
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
                SaveCurrentChat();
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
                ChatHistoryList.IsEnabled = true;
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
            ChatHistoryList.IsEnabled = false;

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

                string documentId = json.RootElement.GetProperty("document_id").GetString()
                    ?? throw new Exception("The backend returned an empty document ID.");

                selectedDocumentIds.Add(documentId);

                AddChatMessage(
                    "Document uploaded",
                    System.IO.Path.GetFileName(dialog.FileName));
                SaveCurrentChat();
            }
            catch (Exception exc)
            {
                AddChatMessage("Upload Failed", exc.Message);
            }
            finally
            {
                AddFileButton.IsEnabled = true;
                SendButton.IsEnabled = true;
                ChatHistoryList.IsEnabled = true;
            }
        }

        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SendButton.IsEnabled)
            {
                return;
            }

            currentChat = null;
            ChatHistoryList.SelectedItem = null;

            conversation.Clear();
            ChatMessagePanel.Children.Clear();
            InputTextBox.Clear();
            selectedDocumentIds.Clear();

            InventoryView.Visibility = Visibility.Collapsed;
            ChatView.Visibility = Visibility.Visible;
            pagetitle.Text = "Paper Desk";

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

            foreach (DocumentItem document in DocumentList.Items)
            {
                if (selectedDocumentIds.Contains(document.document_id))
                {
                    DocumentList.SelectedItems.Add(document);
                }
            }

            ChatView.Visibility = Visibility.Collapsed;
            InventoryView.Visibility = Visibility.Visible;
            pagetitle.Text = "Document Inventory";
        }

        // save chat into history
        private void UseDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            if (DocumentList.SelectedItems.Count == 0)
            {
                return;
            }

            selectedDocumentIds.Clear();

            foreach (DocumentItem document in DocumentList.SelectedItems)
            {
                selectedDocumentIds.Add(document.document_id);
                AddChatMessage("Document selected", document.filename);
            }

            InventoryView.Visibility = Visibility.Collapsed;
            ChatView.Visibility = Visibility.Visible;
            pagetitle.Text = "Paper Flow";

            SaveCurrentChat();
            InputTextBox.Focus();
        }

        public class SavedChat
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public string? DocumentId { get; set; }
            public List<string> DocumentIds { get; set; } = new();
            public List<ChatMessage> Messages { get; set; } = new();
        }

        private List<SavedChat> savedChats = new();
        private SavedChat? currentChat;

        private readonly string historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaperFlow",
            "chats.json");

        private void SaveCurrentChat()
        {
            if (conversation.Count == 0 && selectedDocumentIds.Count == 0)
            {
                return;
            }

            if (currentChat is null)
            {
                currentChat = new SavedChat
                {
                    Id = Guid.NewGuid().ToString()
                };

                savedChats.Insert(0, currentChat);
            }

            string title = conversation.Count > 0
                ? conversation[0].content
                : "Chat with document";

            currentChat.Title = title.Length > 40
                ? title.Substring(0, 40) + "…"
                : title;

            currentChat.DocumentIds = selectedDocumentIds.ToList();
            currentChat.DocumentId = null;
            currentChat.Messages = conversation.ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);

            string json = JsonSerializer.Serialize(
                savedChats,
                new JsonSerializerOptions { WriteIndented = true });

            string temporaryPath = historyPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, historyPath, overwrite: true);

            ChatHistoryList.ItemsSource = null;
            ChatHistoryList.ItemsSource = savedChats;
        }

        //open saved conversation
        private void ChatHistoryList_SelectionChanged( object sender, SelectionChangedEventArgs e)
        {
            if (ChatHistoryList.SelectedItem is not SavedChat chat)
            {
                return;
            }

            currentChat = chat;
            selectedDocumentIds = chat.DocumentIds.ToList();

            // Restore the document from chats saved with the earlier version.
            if (selectedDocumentIds.Count == 0 && chat.DocumentId is not null)
            {
                selectedDocumentIds.Add(chat.DocumentId);
            }

            conversation.Clear();
            conversation.AddRange(chat.Messages);

            ChatMessagePanel.Children.Clear();

            foreach (ChatMessage message in conversation)
            {
                string speaker = message.role == "user" ? "You" : "PaperFlow";
                AddChatMessage(speaker, message.content);
            }

            InventoryView.Visibility = Visibility.Collapsed;
            ChatView.Visibility = Visibility.Visible;
            pagetitle.Text = chat.Title;
            InputTextBox.Clear();
        }

        private void DeleteChatButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SendButton.IsEnabled)
            {
                return;
            }

            if (ChatHistoryList.SelectedItem is not SavedChat chat)
            {
                return;
            }

            savedChats.Remove(chat);

            if (currentChat == chat)
            {
                NewChatButton_Click(sender, e);
            }

            string json = JsonSerializer.Serialize(
                savedChats,
                new JsonSerializerOptions { WriteIndented = true });

            string temporaryPath = historyPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, historyPath, overwrite: true);

            ChatHistoryList.ItemsSource = null;
            ChatHistoryList.ItemsSource = savedChats;
        }
    }
}