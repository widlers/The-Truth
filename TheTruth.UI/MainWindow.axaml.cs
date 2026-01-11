using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TheTruth.UI;

public partial class MainWindow : Window
{
    private readonly TheTruth.Core.VerificationService _service;

    public MainWindow()
    {
        InitializeComponent();
        _service = new TheTruth.Core.VerificationService();
    }

    public async void OnCheckClick(object? sender, RoutedEventArgs e)
    {
        string query = InputBox.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        // Get Category
        var selectedItem = CategoryBox.SelectedItem as ComboBoxItem;
        string category = selectedItem?.Tag?.ToString() ?? "general";

        // Get Language
        var selectedLang = LanguageBox.SelectedItem as ComboBoxItem;
        string language = selectedLang?.Tag?.ToString() ?? "de";

        ResultTextBlock.Text = $"Analyzing ({category} / {language}): " + query + "...\n(Web Search & AI Analysis running...)";
        CheckButton.IsEnabled = false;

        try
        {
            var result = await _service.VerifyAsync(query, category, language);

            ResultTextBlock.Text = "=== AI VERDICT ===\n\n";
            ResultTextBlock.Text += result.AiSummary + "\n\n";
            ResultTextBlock.Text += "==================\n\n";
            ResultTextBlock.Text += $"Based on {result.Sources.Count} Sources:\n\n";

            foreach (var doc in result.Sources)
            {
                ResultTextBlock.Text += $"• {doc.Title}\n  {doc.Url}\n\n";
            }
        }
        catch (System.Exception ex)
        {
            ResultTextBlock.Text = "Error: " + ex.Message;
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }
}