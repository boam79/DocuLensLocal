using System.IO;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DocuLensLocal.Core;

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
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        try
        {
            await TryPrepareOcrAndBackfillAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"본문 인덱스를 시작하지 못했습니다: {ex.Message}";
        }
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

    private void NavTab_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
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
        InfoPanel.IsVisible = showInfo;
        if (showInfo)
        {
            FirstRunPanel.IsVisible = false;
            SearchPanel.IsVisible = false;
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
        FirstRunPanel.IsVisible = true;
        SearchPanel.IsVisible = false;
        InfoPanel.IsVisible = false;
        ShowSavedFolder();
        UpdateIndexButtonState();
    }

    private void ShowMainSearch(bool resetSearch)
    {
        FirstRunPanel.IsVisible = false;
        SearchPanel.IsVisible = true;
        InfoPanel.IsVisible = false;
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

    private async void SelectFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "인덱싱할 폴더를 선택하세요",
            AllowMultiple = false,
        }).ConfigureAwait(true);

        var folder = folders.FirstOrDefault();
        var path = folder?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var settings = LoadSettings();
        settings.IndexFolder = path;
        SaveSettings(settings);
        ShowSavedFolder();
        UpdateIndexButtonState();
        IndexStatusText.Text = "폴더를 선택했습니다. 인덱싱을 누르면 시작합니다.";
        IndexCountText.Text = "건수: —";
        IndexCurrentFileText.Text = "현재 파일: —";
    }

    private async void IndexButton_OnClick(object? sender, RoutedEventArgs e)
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
            await TessdataInstaller.EnsureUserDataAsync().ConfigureAwait(true);
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
            : string.IsNullOrWhiteSpace(progress.PhaseKo) ? "인덱싱 중…" : progress.PhaseKo;
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

    private void SearchButton_OnClick(object? sender, RoutedEventArgs e) => RunSearch();

    private void SearchQueryBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
    }

    private void ChangeFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _showMainSearch = false;
        SearchTab.IsChecked = true;
        ShowFirstRun();
        IndexStatusText.Text = "폴더를 바꾼 뒤 인덱싱을 누르면 다시 시작합니다. 폴더만 고르면 인덱싱은 시작하지 않습니다.";
    }

    private async void UpdateButton_OnClick(object? sender, RoutedEventArgs e)
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
        IReadOnlyList<SearchResultRow> rows;
        if (string.IsNullOrWhiteSpace(query))
        {
            rows = all.Select(doc => new SearchResultRow
            {
                FileName = Path.GetFileName(doc.FilePath),
                FilePath = doc.FilePath,
                Snippet = EvidenceSnippet.From(doc.BodyText, []),
                MatchLabel = doc.OcrPageCount > 0 ? "OCR 포함" : (string.IsNullOrWhiteSpace(doc.BodyText) ? "파일명만" : "본문 인덱스"),
                HasSnippet = !string.IsNullOrWhiteSpace(doc.BodyText),
            }).ToList();
        }
        else
        {
            rows = _indexing.Search(query).Select(hit => new SearchResultRow
            {
                FileName = Path.GetFileName(hit.Document.FilePath),
                FilePath = hit.Document.FilePath,
                Snippet = hit.Snippet,
                MatchLabel = hit.MatchLabelKo,
                HasSnippet = !string.IsNullOrWhiteSpace(hit.Snippet),
            }).ToList();
        }

        IndexedSummaryText.Text = FormatCoverage(all);
        SearchResultsList.ItemsSource = rows;

        if (rows.Count > 0)
        {
            SearchEmptyText.IsVisible = false;
            return;
        }

        var coverage = _indexing.GetCoverage();
        if (all.Count == 0)
        {
            SearchEmptyText.Text = SearchStatusFormatter.EmptyResults(0, 0, _isIndexing);
        }
        else
        {
            SearchEmptyText.Text = SearchStatusFormatter.EmptyResults(all.Count, coverage.BodyCount, _isIndexing);
        }

        SearchEmptyText.IsVisible = true;
    }

    private static string FormatCoverage(IReadOnlyList<IndexedDocument> all)
    {
        var coverage = new IndexCoverage(
            all.Count,
            all.Count(doc => !string.IsNullOrWhiteSpace(doc.BodyText)),
            all.Sum(doc => doc.OcrPageCount),
            CompositeOcrEngine.CreateDefault().IsAvailable);
        return SearchStatusFormatter.Coverage(coverage);
    }

    private async Task TryPrepareOcrAndBackfillAsync()
    {
        try
        {
            await TessdataInstaller.EnsureUserDataAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Digital PDF body search still works without tessdata.
        }

        if (_isIndexing)
        {
            return;
        }

        var settings = LoadSettings();
        if (!IndexBackfillPolicy.ShouldBackfill(_indexing.GetCoverage(), settings.IndexFolder))
        {
            if (SearchPanel.IsVisible)
            {
                RunSearch();
            }

            return;
        }

        var folder = settings.IndexFolder!;
        _isIndexing = true;
        UpdateIndexButtonState();
        IndexedSummaryText.Text = "본문 읽는 중…";
        if (SearchPanel.IsVisible && string.IsNullOrWhiteSpace(SearchQueryBox.Text))
        {
            RunSearch();
        }

        var progress = new Progress<IndexingProgress>(snapshot =>
        {
            IndexedSummaryText.Text = SearchStatusFormatter.CoverageProgress(snapshot.ProcessedCount, snapshot.FoundCount);
            IndexCountText.Text = $"건수: {snapshot.ProcessedCount} / {snapshot.FoundCount}";
            IndexCurrentFileText.Text = string.IsNullOrWhiteSpace(snapshot.CurrentFile)
                ? "현재 파일: —"
                : $"현재 파일: {Path.GetFileName(snapshot.CurrentFile)}";
        });

        try
        {
            await _indexing.Start(folder, progress, CancellationToken.None).ConfigureAwait(true);
            settings.IndexCompleted = true;
            SaveSettings(settings);
            _showMainSearch = true;
            if (SearchTab.IsChecked == true)
            {
                ShowMainSearch(resetSearch: false);
            }
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"본문을 읽지 못했습니다: {ex.Message}";
        }
        finally
        {
            _isIndexing = false;
            UpdateIndexButtonState();
        }
    }

    private void SearchResultsList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not SearchResultRow row || !File.Exists(row.FilePath))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(row.FilePath)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            SearchEmptyText.Text = $"파일을 열지 못했습니다: {ex.Message}";
            SearchEmptyText.IsVisible = true;
        }
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
        public string Snippet { get; init; } = string.Empty;
        public string MatchLabel { get; init; } = string.Empty;
        public bool HasSnippet { get; init; }
    }

    private sealed class HistoryRow
    {
        public required string Version { get; init; }
        public required string SummaryKo { get; init; }
        public bool IsCurrent { get; init; }
        public bool IsLast { get; init; }
    }
}
