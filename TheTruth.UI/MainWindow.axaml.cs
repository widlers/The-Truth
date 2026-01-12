using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input; // For DragDrop
using System.Linq; // Added for FirstOrDefault
using System.Collections.ObjectModel; // For ObservableCollection
using System;

namespace TheTruth.UI;

public partial class MainWindow : Window
{
    private readonly TheTruth.Core.VerificationService _service;
    private ObservableCollection<TheTruth.Core.Models.SearchResultItem> _feedItems = new();
    private int _currentFeedOffset = 0;

    public MainWindow()
    {
        InitializeComponent();
        _service = new TheTruth.Core.VerificationService();

        // Defer initial UI update and selection until window is opened to ensure NameScope is ready
        this.Opened += (s, e) =>
        {
            var langBox = this.FindControl<ComboBox>("LanguageBox");
            if (langBox != null) langBox.SelectedIndex = 0; // This triggers OnLanguageChanged -> UpdateCategories
        };

        // Attach Drag & Drop Events manually
        var dropZone = this.FindControl<Border>("DropZone");
        if (dropZone != null)
        {
            DragDrop.SetAllowDrop(dropZone, true);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
        }

        // Attach Button Events
        var selectBtn = this.FindControl<Button>("SelectFileButton");
        if (selectBtn != null) selectBtn.Click += OnSelectImageClick;

    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Using Data to avoid breaking changes if DataTransfer is not easily available or behaves differently
        // Ignoring obsolete warning for now to prioritize build success.
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            var file = files?.FirstOrDefault();
            if (file != null)
            {
                // Check if image extension
                var path = file.Path.LocalPath; // Ensure LocalPath
                if (IsImageFile(path))
                {
                    LoadImage(path);
                }
            }
        }
    }

    private bool IsImageFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLower();
        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".webp";
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        var langBox = sender as ComboBox;
        if (langBox?.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            UpdateCategories(lang);
        }
    }

    private void UpdateCategories(string lang)
    {
        var categoryBox = this.FindControl<ComboBox>("CategoryBox");
        if (categoryBox == null) return;

        categoryBox.Items.Clear();

        var categories = lang == "de"
            ? new[] { "Allgemein", "Politik", "Wissenschaft", "Gesundheit", "Technologie" }
            : new[] { "General", "Politics", "Science", "Health", "Technology" };

        foreach (var cat in categories)
        {
            categoryBox.Items.Add(new ComboBoxItem { Content = cat, Tag = cat.ToLower() });
        }
        categoryBox.SelectedIndex = 0;
    }

    public async void OnCheckClick(object? sender, RoutedEventArgs e)
    {
        var inputBox = this.FindControl<TextBox>("InputBox");
        var categoryBox = this.FindControl<ComboBox>("CategoryBox");
        var languageBox = this.FindControl<ComboBox>("LanguageBox");
        var verdictText = this.FindControl<TextBlock>("AiVerdictText");
        var sourcesList = this.FindControl<ItemsControl>("SourcesList");
        var checkButton = this.FindControl<Button>("CheckButton");
        var sourcesHeader = this.FindControl<TextBlock>("SourcesHeader");

        if (inputBox == null || verdictText == null || checkButton == null) return;

        string query = inputBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(query)) return;

        checkButton.IsEnabled = false;
        verdictText.Text = "Verifying claim with AI agents... (This usually takes 5-10 seconds)";

        string lang = "en";
        if (languageBox?.SelectedItem is ComboBoxItem lItem && lItem.Tag is string lTag) lang = lTag;

        string category = "general";
        if (categoryBox?.SelectedItem is ComboBoxItem cItem && cItem.Tag is string cTag) category = cTag;

        try
        {
            var result = await _service.VerifyAsync(query, category, lang);

            verdictText.Text = result.AiSummary;

            if (sourcesList != null)
            {
                sourcesList.ItemsSource = result.Sources;
                if (sourcesHeader != null) sourcesHeader.IsVisible = result.Sources.Count > 0;
            }
        }
        catch (Exception ex)
        {
            verdictText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            checkButton.IsEnabled = true;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        var inputBox = this.FindControl<TextBox>("InputBox");
        var verdictText = this.FindControl<TextBlock>("AiVerdictText");
        var sourcesList = this.FindControl<ItemsControl>("SourcesList");
        var sourcesHeader = this.FindControl<TextBlock>("SourcesHeader");

        if (inputBox != null) inputBox.Text = string.Empty;
        if (verdictText != null) verdictText.Text = "Ready to verify.";
        if (sourcesList != null) sourcesList.ItemsSource = null;
        if (sourcesHeader != null) sourcesHeader.IsVisible = false;
    }

    private async void OnRefreshFeed(object sender, RoutedEventArgs e)
    {
        var feedList = this.FindControl<ItemsControl>("FeedList");
        var sourceBox = this.FindControl<ComboBox>("SourceBox");
        var languageBox = this.FindControl<ComboBox>("LanguageBox");
        var loadMoreBtn = this.FindControl<Button>("LoadMoreButton");

        if (feedList == null) return;

        string lang = "en";
        if (languageBox?.SelectedItem is ComboBoxItem lItem && lItem.Tag is string lTag) lang = lTag;

        string source = "all";
        if (sourceBox?.SelectedItem is ComboBoxItem sItem && sItem.Tag is string sTag) source = sTag;

        // Reset Pagination
        _currentFeedOffset = 0;
        _feedItems.Clear();
        feedList.ItemsSource = _feedItems;

        // Disable button during load
        if (loadMoreBtn != null)
        {
            loadMoreBtn.IsEnabled = false;
            loadMoreBtn.Content = "Loading...";
            loadMoreBtn.IsVisible = true;
        }

        try
        {
            var feed = await _service.GetLiveFeedAsync(lang, source, _currentFeedOffset);
            foreach (var item in feed)
            {
                _feedItems.Add(item);
            }
        }
        finally
        {
            if (loadMoreBtn != null)
            {
                loadMoreBtn.IsEnabled = true;
                loadMoreBtn.Content = "Load More";
                // Hide if no results? For now keep visible if list not empty
                loadMoreBtn.IsVisible = _feedItems.Count > 0;
            }
        }
    }

    private async void OnLoadMoreFeed(object sender, RoutedEventArgs e)
    {
        var loadMoreBtn = sender as Button;
        var sourceBox = this.FindControl<ComboBox>("SourceBox");
        var languageBox = this.FindControl<ComboBox>("LanguageBox");

        if (loadMoreBtn != null) { loadMoreBtn.IsEnabled = false; loadMoreBtn.Content = "Loading..."; }

        string lang = "en";
        if (languageBox?.SelectedItem is ComboBoxItem lItem && lItem.Tag is string lTag) lang = lTag;

        string source = "all";
        if (sourceBox?.SelectedItem is ComboBoxItem sItem && sItem.Tag is string sTag) source = sTag;

        _currentFeedOffset += 20;

        try
        {
            var feed = await _service.GetLiveFeedAsync(lang, source, _currentFeedOffset);

            if (feed.Count == 0)
            {
                if (loadMoreBtn != null)
                {
                    loadMoreBtn.Content = "No more articles";
                    // keep disabled or hide after delay
                }
            }
            else
            {
                foreach (var item in feed)
                {
                    _feedItems.Add(item);
                }

                if (loadMoreBtn != null)
                {
                    loadMoreBtn.IsEnabled = true;
                    loadMoreBtn.Content = "Load More";
                }
            }
        }
        catch
        {
            if (loadMoreBtn != null)
            {
                loadMoreBtn.IsEnabled = true;
                loadMoreBtn.Content = "Retry";
            }
        }
    }

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.Tag is string url)
        {
            OpenUrl(url);
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* ignore errors */ }
    }

    // --- Media Check Logic ---

    private string? _selectedImagePath;
    private Point _dragStartPoint;

    private void OnPreviewImagePointerPressed(object sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Control);
        if (point.Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = point.Position;
        }
    }

    private async void OnPreviewImagePointerMoved(object sender, PointerEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedImagePath)) return;

        var point = e.GetCurrentPoint(sender as Control);
        if (!point.Properties.IsLeftButtonPressed) return;

        var diff = _dragStartPoint - point.Position;
        if (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)
        {
            var data = new DataObject();
            data.Set(DataFormats.FileNames, new[] { _selectedImagePath });

            // DoDragDrop is an extension method or needs sender which is Visual
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        }
    }

    public async void OnSelectImageClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = AccessTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Image to Analyze",
            AllowMultiple = false,
            FileTypeFilter = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
        });

        if (files.Count >= 1)
        {
            LoadImage(files[0].Path.LocalPath);
        }
    }

    private Avalonia.Controls.TopLevel? AccessTopLevel()
    {
        return Avalonia.Controls.TopLevel.GetTopLevel(this);
    }

    private void LoadImage(string path)
    {
        try
        {
            _selectedImagePath = path;
            var previewImage = this.FindControl<Avalonia.Controls.Image>("PreviewImage");
            var verdictText = this.FindControl<TextBox>("MediaVerdictText");
            var analyzeBtn = this.FindControl<Button>("AnalyzeButton");
            var checkMetaBtn = this.FindControl<Button>("CheckMetadataButton");

            if (previewImage != null) previewImage.Source = new Avalonia.Media.Imaging.Bitmap(path);
            if (verdictText != null) verdictText.Text = "Image loaded.\nClick 'ANALYZE MEDIA' to start analysis.";
            if (analyzeBtn != null) analyzeBtn.IsEnabled = true;
            if (checkMetaBtn != null) checkMetaBtn.IsEnabled = true;
        }
        catch (System.Exception ex)
        {
            var verdictText = this.FindControl<TextBox>("MediaVerdictText");
            if (verdictText != null) verdictText.Text = "Error loading image: " + ex.Message;
        }
    }

    public async void OnAnalyzeMediaClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedImagePath)) return;

        var verdictText = this.FindControl<TextBox>("MediaVerdictText");
        var analyzeBtn = this.FindControl<Button>("AnalyzeButton");
        var checkMetaBtn = this.FindControl<Button>("CheckMetadataButton");

        if (analyzeBtn != null) analyzeBtn.IsEnabled = false;
        if (checkMetaBtn != null) checkMetaBtn.IsEnabled = false;
        if (verdictText != null) verdictText.Text = "Analyzing image with local AI (BakLLaVA)...\nThis may take a few seconds...";

        // Get Language
        var langBox = this.FindControl<ComboBox>("LanguageBox");
        string lang = "en";
        if (langBox?.SelectedItem is ComboBoxItem item && item.Tag is string l)
        {
            lang = l;
        }

        try
        {
            var result = await _service.AnalyzeImageAsync(_selectedImagePath, lang);
            if (verdictText != null) verdictText.Text = "=== VISUAL FORENSICS REPORT ===\n\n" + result;
        }
        catch (Exception ex)
        {
            if (verdictText != null) verdictText.Text = "Analysis Failed: " + ex.Message;
        }

        if (analyzeBtn != null) analyzeBtn.IsEnabled = true;
        if (checkMetaBtn != null) checkMetaBtn.IsEnabled = true;
    }

    public async void OnCheckMetadataClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedImagePath)) return;

        var verdictText = this.FindControl<TextBox>("MediaVerdictText");
        var analyzeBtn = this.FindControl<Button>("AnalyzeButton");
        var checkMetaBtn = this.FindControl<Button>("CheckMetadataButton");

        if (analyzeBtn != null) analyzeBtn.IsEnabled = false;
        if (checkMetaBtn != null) checkMetaBtn.IsEnabled = false;

        if (verdictText != null) verdictText.Text = "Extracting Metadata & C2PA Signatures...\nThis runs locally and is 100% private.";

        try
        {
            var resultJson = await _service.AnalyzeMetadataAsync(_selectedImagePath);
            // prettify json for now
            // We could parse it, but raw JSON is honest for metadata
            if (verdictText != null) verdictText.Text = "=== METADATA REPORT (C2PA & EXIF) ===\n\n" + resultJson;
        }
        catch (Exception ex)
        {
            if (verdictText != null) verdictText.Text = "Metadata extraction failed: " + ex.Message;
        }

        if (analyzeBtn != null) analyzeBtn.IsEnabled = true;
        if (checkMetaBtn != null) checkMetaBtn.IsEnabled = true;
    }

    private void OnExternalToolClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url)
        {
            OpenUrl(url);
        }
    }
}