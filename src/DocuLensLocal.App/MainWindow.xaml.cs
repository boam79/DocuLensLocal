using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using DocuLensLocal.Core;
using Microsoft.Win32;

namespace DocuLensLocal.App;

public partial class MainWindow : Window
{
    private readonly IndexingService _indexing = new();
    private readonly AppUpdater _updater = new();
    private readonly IUpdateFeed _updateFeed;
    private bool _isIndexing;
    private bool _showMainSearch;

    public MainWindow()
        : this(new VelopackUpdateFeed())
    {
    }

    public MainWindow(IUpdateFeed updateFeed)
    {
        _updateFeed = updateFeed;
        InitializeComponent();
        LoadInfoPanel();
        ApplyStartupView();
    }

    private void LoadInfoPanel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? "DocuLens Local";
        var version = AppVersionFormatter.DisplayVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version);

        ProductNameText.Text = product;
        VersionText.Text = $"버전 {version}";
        var notes = ReleaseHistory.Known;
        VersionHistoryList.ItemsSource = notes
            .Select((note, i) => new HistoryRow
            {
                Version = note.Version,
                SummaryKo = note.SummaryKo,
                IsCurrent = i == 0,
                IsLast = i == notes.Count - 1,
            })
            .ToList();
    }

    private void ApplyStartupView()
    {
        var settings = LoadSettings();
        var indexedCount = _indexing.GetIndexedDocuments().Count;
        _showMainSearch = StartupViewResolver.Resolve(settings, indexedCount) == StartupView.MainSearch;
        RefreshMainContent(resetSearch: true);
    }

    private void NavTab_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RefreshMainContent(resetSearch: false);
    }

    private void RefreshMainContent(bool resetSearch)
    {
        var showInfo = InfoTab.IsChecked == true;
        InfoPanel.Visibility = showInfo ? Visibility.Visible : Visibility.Collapsed;
        if (showInfo)
        {
            FirstRunPanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (_showMainSearch)
        {
            ShowMainSearch(resetSearch);
            return;
        }

        ShowFirstRun();
    }

    private void ShowFirstRun()
    {
        FirstRunPanel.Visibility = Visibility.Visible;
        SearchPanel.Visibility = Visibility.Collapsed;
        InfoPanel.Visibility = Visibility.Collapsed;
        ShowSavedFolder();
        UpdateIndexButtonState();
    }

    private void ShowMainSearch(bool resetSearch)
    {
        FirstRunPanel.Visibility = Visibility.Collapsed;
        SearchPanel.Visibility = Visibility.Visible;
        InfoPanel.Visibility = Visibility.Collapsed;
        if (resetSearch)
        {
            SearchQueryBox.Text = string.Empty;
        }

        RunSearch();
        if (resetSearch)
        {
            SearchQueryBox.Focus();
        }
    }

    private void ShowSavedFolder()
    {
        var settings = LoadSettings();
        SelectedFolderText.Text = string.IsNullOrWhiteSpace(settings.IndexFolder)
            ? "아직 폴더를 고르지 않았습니다. 폴더를 고르기 전에는 인덱싱을 시작하지 않습니다."
            : $"선택한 폴더: {settings.IndexFolder}";
    }

    private void UpdateIndexButtonState()
    {
        var folder = LoadSettings().IndexFolder;
        IndexButton.IsEnabled = !_isIndexing
            && !string.IsNullOrWhiteSpace(folder)
            && Directory.Exists(folder);
        SelectFolderButton.IsEnabled = !_isIndexing;
    }

    private void SelectFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "인덱싱할 폴더를 선택하세요",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var settings = LoadSettings();
        settings.IndexFolder = dialog.FolderName;
        SaveSettings(settings);
        ShowSavedFolder();
        UpdateIndexButtonState();
        IndexStatusText.Text = "폴더를 선택했습니다. 인덱싱을 누르면 시작합니다.";
        IndexCountText.Text = "건수: —";
        IndexCurrentFileText.Text = "현재 파일: —";
    }

    private async void IndexButton_OnClick(object sender, RoutedEventArgs e)
    {
        var folder = LoadSettings().IndexFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            UpdateIndexButtonState();
            IndexStatusText.Text = "폴더를 먼저 선택하세요.";
            return;
        }

        _isIndexing = true;
        UpdateIndexButtonState();
        IndexStatusText.Text = "인덱싱 중…";
        IndexCountText.Text = "건수: 0 / —";
        IndexCurrentFileText.Text = "현재 파일: —";

        var progress = new Progress<IndexingProgress>(ShowProgress);

        try
        {
            var result = await _indexing.Start(folder, progress, CancellationToken.None).ConfigureAwait(true);
            ShowResult(result);
        }
        catch (Exception ex)
        {
            IndexStatusText.Text = $"인덱싱을 끝내지 못했습니다: {ex.Message}";
        }
        finally
        {
            _isIndexing = false;
            UpdateIndexButtonState();
        }
    }

    private void ShowProgress(IndexingProgress progress)
    {
        IndexCountText.Text = $"건수: {progress.ProcessedCount} / {progress.FoundCount}";
        IndexCurrentFileText.Text = string.IsNullOrWhiteSpace(progress.CurrentFile)
            ? "현재 파일: —"
            : $"현재 파일: {Path.GetFileName(progress.CurrentFile)}";
        IndexStatusText.Text = progress.IsCompleted
            ? FormatCompleteStatus(progress.Errors.Count)
            : "인덱싱 중…";
    }

    private void ShowResult(IndexingResult result)
    {
        if (!result.IsCompleted)
        {
            IndexCountText.Text = $"건수: {result.ProcessedCount} / {result.FoundCount}";
            IndexStatusText.Text = FormatCompleteStatus(result.Errors.Count);
            return;
        }

        var settings = LoadSettings();
        settings.IndexCompleted = true;
        SaveSettings(settings);
        _showMainSearch = true;
        SearchTab.IsChecked = true;
        ShowMainSearch(resetSearch: true);
    }

    private void SearchButton_OnClick(object sender, RoutedEventArgs e) => RunSearch();

    private void SearchQueryBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
    }

    private void ChangeFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        _showMainSearch = false;
        SearchTab.IsChecked = true;
        ShowFirstRun();
        IndexStatusText.Text = "폴더를 바꾼 뒤 인덱싱을 누르면 다시 시작합니다. 폴더만 고르면 인덱싱은 시작하지 않습니다.";
    }

    private async void UpdateButton_OnClick(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "업데이트를 확인하는 중…";

        try
        {
            var result = await _updater.CheckAndApplyAsync(_updateFeed).ConfigureAwait(true);
            UpdateStatusText.Text = result.MessageKo;
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"업데이트를 확인하지 못했습니다: {ex.Message}";
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private void RunSearch()
    {
        var all = _indexing.GetIndexedDocuments();
        var query = SearchQueryBox.Text;
        var matches = string.IsNullOrWhiteSpace(query)
            ? all
            : _indexing.SearchByFileName(query);

        IndexedSummaryText.Text = $"인덱싱 완료 · {all.Count}건";
        SearchResultsList.ItemsSource = matches
            .Select(doc => new SearchResultRow
            {
                FileName = Path.GetFileName(doc.FilePath),
                FilePath = doc.FilePath,
            })
            .ToList();

        if (matches.Count > 0)
        {
            SearchEmptyText.Visibility = Visibility.Collapsed;
            return;
        }

        SearchEmptyText.Text = all.Count == 0
            ? "인덱싱된 PDF가 없습니다. 아래에서 폴더를 바꿔 다시 인덱싱할 수 있습니다."
            : "조건에 맞는 파일이 없습니다.";
        SearchEmptyText.Visibility = Visibility.Visible;
    }

    private static string FormatCompleteStatus(int errorCount) =>
        errorCount > 0 ? $"완료 (오류 {errorCount}건)" : "완료";

    private static AppSettings LoadSettings()
    {
        if (!File.Exists(AppPaths.SettingsFile))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(AppPaths.SettingsFile);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    private static void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.UserData);
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings));
    }

    private sealed class SearchResultRow
    {
        public required string FileName { get; init; }
        public required string FilePath { get; init; }
    }

    private sealed class HistoryRow
    {
        public required string Version { get; init; }
        public required string SummaryKo { get; init; }
        public bool IsCurrent { get; init; }
        public bool IsLast { get; init; }
    }
}
