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
        UpdateCategories("de"); // Default
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryBox == null) return; // Prevention for init

        var box = sender as ComboBox;
        var selectedItem = box?.SelectedItem as ComboBoxItem;
        string lang = selectedItem?.Tag?.ToString() ?? "de";

        // Preserve selected category index if possible
        int oldIndex = CategoryBox.SelectedIndex;
        if (oldIndex < 0) oldIndex = 0;

        UpdateCategories(lang);

        CategoryBox.SelectedIndex = oldIndex;
    }

    private void UpdateCategories(string lang)
    {
        CategoryBox.Items.Clear();

        if (lang == "en")
        {
            CategoryBox.Items.Add(new ComboBoxItem { Content = "General", Tag = "general" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "News & Politics", Tag = "news_politics" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Medicine & Health", Tag = "medicine" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Science & Climate", Tag = "science" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Finance & Economy", Tag = "finance" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Tech & AI", Tag = "tech" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Social Media & Viral", Tag = "social" });
        }
        else
        {
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Allgemein", Tag = "general" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "News & Politik", Tag = "news_politics" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Medizin & Gesundheit", Tag = "medicine" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Wissenschaft & Klima", Tag = "science" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Finanzen & Wirtschaft", Tag = "finance" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Technik & AI", Tag = "tech" });
            CategoryBox.Items.Add(new ComboBoxItem { Content = "Social Media & Viral", Tag = "social" });
        }

        if (CategoryBox.SelectedIndex == -1) CategoryBox.SelectedIndex = 0;
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