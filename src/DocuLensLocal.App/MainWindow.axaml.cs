using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DocuLensLocal.Core;

namespace DocuLensLocal.App;

public partial class MainWindow : Window
{
    private readonly IndexingService _indexing = new();
    private readonly AppUpdater _updater = new();
    private readonly IUpdateFeed _updateFeed;
    private CancellationTokenSource? _indexCts;
    private Task? _indexingTask;
    private bool _isIndexing;
    private bool _showMainSearch;
    private bool _searchSubmitted;
    private bool _resumeIndexingAfterUpdate;
    private bool _pendingWatchSync;
    private int _watchRetryCount;
    private SearchFormatFilter _formatFilter = SearchFormatFilter.All;
    private readonly FolderIndexWatch _folderWatch;

    public MainWindow()
        : this(new VelopackUpdateFeed())
    {
    }

    public MainWindow(IUpdateFeed updateFeed)
    {
        _updateFeed = updateFeed;
        InitializeComponent();
        ApplyFormatFilterVisuals();
        _folderWatch = new FolderIndexWatch(IndexWatchPolicy.Debounce, () =>
        {
            Dispatcher.UIThread.Post(() => _ = TryWatchSyncAsync());
        });
        LoadInfoPanel();
        ApplyStartupView();
        Opened += OnOpened;
        Closed += OnWindowClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        try
        {
            await TryShowPendingUpdateNotesAsync().ConfigureAwait(true);
            await TryPromptForUpdateAsync().ConfigureAwait(true);
            await TryResumeOrBackfillIndexingAsync().ConfigureAwait(true);
            RefreshFolderWatch();
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"시작하지 못했습니다: {ex.Message}";
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        _folderWatch.Dispose();
    }

    private async Task TryShowPendingUpdateNotesAsync()
    {
        var settings = LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.PendingUpdateNotes))
        {
            return;
        }

        var notes = settings.PendingUpdateNotes;
        settings.PendingUpdateNotes = null;
        settings.PendingUpdateVersion = null;
        SaveSettings(settings);
        await MessageDialog.AlertAsync(this, UpdatePromptCopy.NotesTitle, notes).ConfigureAwait(true);
    }

    private async Task TryPromptForUpdateAsync()
    {
        var result = await _updater.CheckAsync(_updateFeed).ConfigureAwait(true);
        if (result.Status == AppUpdateStatus.Available && !string.IsNullOrWhiteSpace(result.NewerVersion))
        {
            var confirm = await MessageDialog.ConfirmAsync(
                this,
                UpdatePromptCopy.AvailableTitle,
                UpdatePromptCopy.AvailableBody(result.NewerVersion, IndexingWouldResume())).ConfigureAwait(true);
            if (!confirm)
            {
                return;
            }

            await ApplyConfirmedUpdateAsync(result.NewerVersion).ConfigureAwait(true);
            return;
        }

        if (result.Status == AppUpdateStatus.NotPackaged && !string.IsNullOrWhiteSpace(result.NewerVersion))
        {
            await MessageDialog.AlertAsync(this, UpdatePromptCopy.AvailableTitle, result.MessageKo).ConfigureAwait(true);
        }
    }

    private async Task ApplyConfirmedUpdateAsync(string version)
    {
        CancelIndexingForUpdate();
        var running = _indexingTask;
        if (running is not null)
        {
            try
            {
                await running.ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Indexing stopped so the update can replace files. Resume after restart.
            }
        }

        var settings = LoadSettings();
        if (_resumeIndexingAfterUpdate || settings.IndexingInProgress)
        {
            IndexingRunState.OnStarted(settings);
        }
        settings.PendingUpdateVersion = version;
        settings.PendingUpdateNotes = ReleaseHistory.FormatNotes(CurrentDisplayVersion(), version);
        SaveSettings(settings);
        var applied = await _updater.ApplyAsync(_updateFeed, version).ConfigureAwait(true);
        UpdateStatusText.Text = applied.MessageKo;
        if (applied.Status != AppUpdateStatus.Applied)
        {
            settings.PendingUpdateNotes = null;
            settings.PendingUpdateVersion = null;
            SaveSettings(settings);
            await MessageDialog.AlertAsync(this, UpdatePromptCopy.AvailableTitle, applied.MessageKo).ConfigureAwait(true);
            if (!_isIndexing && IndexResumePolicy.ShouldResume(LoadSettings()))
            {
                await TryResumeOrBackfillIndexingAsync().ConfigureAwait(true);
            }
        }
    }

    private bool IndexingWouldResume()
    {
        var settings = LoadSettings();
        return _isIndexing || _resumeIndexingAfterUpdate || IndexResumePolicy.ShouldResume(settings);
    }

    private void CancelIndexingForUpdate()
    {
        if (_isIndexing)
        {
            _resumeIndexingAfterUpdate = true;
            var settings = LoadSettings();
            IndexingRunState.OnStarted(settings);
            SaveSettings(settings);
        }

        _indexCts?.Cancel();
    }

    private static string CurrentDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return AppVersionFormatter.DisplayVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version);
    }

    private void LoadInfoPanel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = AppVersionFormatter.DisplayVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version);

        ProductNameText.Text = InfoStatusCopy.Headline;
        VersionText.Text = $"버전 {version}";
        VersionHistoryList.ItemsSource = HistoryRows(ReleaseHistory.Recent(), markCurrent: true);
        var older = ReleaseHistory.Older();
        OlderVersionHistoryList.ItemsSource = HistoryRows(older, markCurrent: false);
        OlderHistoryExpander.IsVisible = older.Count > 0;
        RefreshInfoPanel();
    }

    private void RefreshInfoPanel()
    {
        var folder = LoadSettings().IndexFolder;
        InfoFolderPathText.Text = InfoStatusCopy.FolderLine(folder);
        var canOpen = !string.IsNullOrWhiteSpace(folder);
        InfoFolderButton.IsEnabled = canOpen;
        ToolTip.SetTip(InfoFolderButton, canOpen ? folder : null);
        var coverage = CoverageOf(_indexing.GetIndexedDocuments());
        InfoDocumentCountText.Text = InfoStatusCopy.DocumentCount(coverage);
        InfoBodyCountText.Text = InfoStatusCopy.BodyLabel(coverage);
        InfoOcrCountText.Text = InfoStatusCopy.OcrLabel(coverage);
    }

    private static List<HistoryRow> HistoryRows(IReadOnlyList<ReleaseNote> notes, bool markCurrent) =>
        notes.Select((note, i) => new HistoryRow
        {
            Version = note.Version,
            SummaryKo = note.SummaryKo,
            IsCurrent = markCurrent && i == 0,
            IsLast = i == notes.Count - 1,
        }).ToList();

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
            RefreshInfoPanel();
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
            _searchSubmitted = false;
        }

        if (_searchSubmitted)
        {
            RunSearch();
        }
        else
        {
            ShowIdleSearch();
        }

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
        FolderMenuButton.IsEnabled = !_isIndexing;
        ChangeFolderMenuItem.IsEnabled = !_isIndexing;
        var folderReady = !_isIndexing
            && !string.IsNullOrWhiteSpace(folder)
            && Directory.Exists(folder);
        SyncIndexMenuItem.IsEnabled = folderReady;
        RebuildIndexMenuItem.IsEnabled = folderReady;
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
        _folderWatch.Stop();
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
        BeginIndexingCancellation();
        MarkIndexingStarted();

        var progress = new Progress<IndexingProgress>(ShowProgress);

        try
        {
            await TessdataInstaller.EnsureUserDataAsync().ConfigureAwait(true);
            var indexing = _indexing.Start(folder, progress, IndexingToken());
            _indexingTask = indexing;
            var result = await indexing.ConfigureAwait(true);
            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            IndexStatusText.Text = _resumeIndexingAfterUpdate
                ? "업데이트가 끝나면 인덱싱을 이어서 합니다."
                : "인덱싱을 잠시 멈췄습니다. 다시 켜면 남은 파일부터 이어서 읽습니다.";
        }
        catch (Exception ex)
        {
            IndexStatusText.Text = $"인덱싱을 끝내지 못했습니다: {ex.Message}";
        }
        finally
        {
            _indexingTask = null;
            _isIndexing = false;
            UpdateIndexButtonState();
            RefreshFolderWatch();
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
        IndexingRunState.OnFinished(settings, completed: true);
        SaveSettings(settings);
        _showMainSearch = true;
        SearchTab.IsChecked = true;
        ShowMainSearch(resetSearch: true);
        RefreshFolderWatch();
    }

    private void SearchButton_OnClick(object? sender, RoutedEventArgs e) => RunSearch();

    private void ResetSearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SearchQueryBox.Text = string.Empty;
        _searchSubmitted = false;
        ShowIdleSearch();
        SearchQueryBox.Focus();
    }

    private void ExampleChip_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string query || string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        SearchQueryBox.Text = query;
        RunSearch();
    }

    private void FormatFilterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not string tag
            || !Enum.TryParse(tag, ignoreCase: true, out SearchFormatFilter clicked)
            || clicked == SearchFormatFilter.All)
        {
            return;
        }

        _formatFilter = SearchFormatFilters.Toggle(_formatFilter, clicked);
        ApplyFormatFilterVisuals();
        if (_searchSubmitted)
        {
            RunSearch();
        }
    }

    private void ApplyFormatFilterVisuals()
    {
        SetFormatSelected(PdfFormatButton, SearchFormatFilter.Pdf);
        SetFormatSelected(WordFormatButton, SearchFormatFilter.Word);
        SetFormatSelected(HangulFormatButton, SearchFormatFilter.Hangul);
        SetFormatSelected(ExcelFormatButton, SearchFormatFilter.Excel);
        FormatFilterHintText.Text = SearchFormatFilters.Hint(_formatFilter);
        FormatFilterHintText.IsVisible = _formatFilter != SearchFormatFilter.All;
    }

    private void SetFormatSelected(Button button, SearchFormatFilter format)
    {
        var selected = SearchFormatFilters.Includes(_formatFilter, format);
        button.Classes.Set("selected", selected);
        button.Opacity = _formatFilter != SearchFormatFilter.All && !selected ? 0.45 : 1;
    }

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
        _folderWatch.Stop();
        _showMainSearch = false;
        SearchTab.IsChecked = true;
        ShowFirstRun();
        IndexStatusText.Text = "폴더를 바꾼 뒤 인덱싱을 누르면 그 폴더로 목록을 맞춥니다. 폴더만 고르면 인덱싱은 시작하지 않습니다.";
    }

    private void IndexedFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = LoadSettings().IndexFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            ShowFileActionError("폴더를 열 수 없습니다", "폴더가 없거나 옮겨졌습니다. 「폴더」에서 폴더를 바꿔 보세요.");
            return;
        }

        try
        {
            Process.Start(LocalFileActions.Open(folder));
        }
        catch (Exception ex)
        {
            ShowFileActionError("폴더를 열 수 없습니다", ex.Message);
        }
    }

    private async void SyncIndexButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = LoadSettings().IndexFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            _showMainSearch = false;
            SearchTab.IsChecked = true;
            ShowFirstRun();
            IndexStatusText.Text = "폴더를 먼저 선택한 뒤 인덱싱하세요.";
            return;
        }

        if (_isIndexing)
        {
            return;
        }

        var plan = _indexing.PlanSync(folder);
        if (!plan.NeedsWork)
        {
            _showMainSearch = true;
            SearchTab.IsChecked = true;
            ShowMainSearch(resetSearch: false);
            IdleHintText.Text = "새로 넣은 파일이 없습니다. 이미 읽은 파일은 그대로 둡니다.";
            return;
        }

        await RunIndexingPassAsync(folder, IndexPass.NewAndChanged, "새로 넣은 파일만 읽는 중…").ConfigureAwait(true);
    }

    private async Task RunIndexingPassAsync(string folder, IndexPass pass, string startMessage, bool preserveSearch = false)
    {
        if (_isIndexing)
        {
            _pendingWatchSync = true;
            return;
        }

        _isIndexing = true;
        UpdateIndexButtonState();
        if (!preserveSearch)
        {
            _searchSubmitted = false;
            SearchQueryBox.Text = string.Empty;
            SearchResultsList.ItemsSource = null;
            ApplySearchListMode(SearchListMode.Idle);
            IdleHintText.Text = pass == IndexPass.NewAndChanged
                ? "이미 읽은 파일은 그대로 두고, 새로 넣거나 바뀐 파일만 읽습니다."
                : "파일명이나 본문 단어로 찾아 보세요";
        }

        IndexedSummaryText.Text = startMessage;
        BeginIndexingCancellation();
        MarkIndexingStarted();

        var progress = new Progress<IndexingProgress>(snapshot =>
        {
            IndexedSummaryText.Text = pass == IndexPass.NewAndChanged
                ? SearchStatusFormatter.NewFilesProgress(snapshot.ProcessedCount, snapshot.FoundCount)
                : SearchStatusFormatter.CoverageProgress(snapshot.ProcessedCount, snapshot.FoundCount);
        });

        try
        {
            await TessdataInstaller.EnsureUserDataAsync().ConfigureAwait(true);
            var indexing = _indexing.Start(folder, progress, IndexingToken(), pass);
            _indexingTask = indexing;
            var result = await indexing.ConfigureAwait(true);
            var settings = LoadSettings();
            IndexingRunState.OnFinished(settings, completed: result.IsCompleted);
            SaveSettings(settings);
            _showMainSearch = true;
            if (SearchTab.IsChecked == true)
            {
                if (preserveSearch)
                {
                    if (_searchSubmitted)
                    {
                        RunSearch();
                    }
                    else
                    {
                        ShowIdleSearch();
                    }
                }
                else
                {
                    ShowMainSearch(resetSearch: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            IndexedSummaryText.Text = _resumeIndexingAfterUpdate
                ? "업데이트가 끝나면 인덱싱을 이어서 합니다."
                : "인덱싱을 잠시 멈췄습니다. 다시 켜면 남은 파일부터 이어서 읽습니다.";
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"인덱싱을 끝내지 못했습니다: {ex.Message}";
        }
        finally
        {
            _indexingTask = null;
            _isIndexing = false;
            UpdateIndexButtonState();
            RefreshFolderWatch();
        }

        if (_pendingWatchSync)
        {
            _pendingWatchSync = false;
            await TryWatchSyncAsync().ConfigureAwait(true);
        }
    }

    private async void RebuildIndexButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = LoadSettings().IndexFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            _showMainSearch = false;
            SearchTab.IsChecked = true;
            ShowFirstRun();
            IndexStatusText.Text = "폴더를 먼저 선택한 뒤 인덱싱하세요.";
            return;
        }

        if (_isIndexing)
        {
            return;
        }

        var confirm = await MessageDialog.ConfirmAsync(
            this,
            "처음부터 다시 읽기",
            "원본 파일은 그대로입니다. 이 앱의 검색 목록만 지우고 폴더를 처음부터 다시 읽습니다.",
            "다시 읽기",
            "취소").ConfigureAwait(true);
        if (!confirm)
        {
            return;
        }

        _isIndexing = true;
        UpdateIndexButtonState();
        _searchSubmitted = false;
        SearchQueryBox.Text = string.Empty;
        SearchResultsList.ItemsSource = null;
        ApplySearchListMode(SearchListMode.Idle);
        IndexedSummaryText.Text = "검색 목록을 지우고 다시 읽는 중…";
        IdleHintText.Text = "원본 파일은 그대로 두고, 이 앱의 검색 목록만 다시 만듭니다.";
        BeginIndexingCancellation();
        MarkIndexingStarted();

        var progress = new Progress<IndexingProgress>(snapshot =>
        {
            IndexedSummaryText.Text = snapshot.IsCompleted
                ? SearchStatusFormatter.CoverageProgress(snapshot.ProcessedCount, snapshot.FoundCount)
                : $"{snapshot.PhaseKo ?? "인덱싱 중"} · {snapshot.ProcessedCount} / {snapshot.FoundCount}";
        });

        try
        {
            await TessdataInstaller.EnsureUserDataAsync().ConfigureAwait(true);
            var indexing = _indexing.Rebuild(folder, progress, IndexingToken());
            _indexingTask = indexing;
            var result = await indexing.ConfigureAwait(true);
            var settings = LoadSettings();
            IndexingRunState.OnFinished(settings, completed: result.IsCompleted);
            SaveSettings(settings);
            _showMainSearch = true;
            if (SearchTab.IsChecked == true)
            {
                ShowMainSearch(resetSearch: true);
            }
        }
        catch (OperationCanceledException)
        {
            IndexedSummaryText.Text = _resumeIndexingAfterUpdate
                ? "업데이트가 끝나면 인덱싱을 이어서 합니다."
                : "다시 인덱싱을 잠시 멈췄습니다. 다시 켜면 이어서 읽습니다.";
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"다시 인덱싱하지 못했습니다: {ex.Message}";
        }
        finally
        {
            _indexingTask = null;
            _isIndexing = false;
            UpdateIndexButtonState();
            RefreshFolderWatch();
        }
    }

    private async void UpdateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "업데이트를 확인하는 중…";

        try
        {
            var result = await _updater.CheckAsync(_updateFeed).ConfigureAwait(true);
            UpdateStatusText.Text = result.MessageKo;
            if (result.Status == AppUpdateStatus.Available && !string.IsNullOrWhiteSpace(result.NewerVersion))
            {
                var confirm = await MessageDialog.ConfirmAsync(
                    this,
                    UpdatePromptCopy.AvailableTitle,
                    UpdatePromptCopy.AvailableBody(result.NewerVersion, IndexingWouldResume())).ConfigureAwait(true);
                if (!confirm)
                {
                    UpdateStatusText.Text = "업데이트를 나중에 설치할 수 있습니다.";
                    return;
                }

                await ApplyConfirmedUpdateAsync(result.NewerVersion).ConfigureAwait(true);
                return;
            }

            if (result.Status is AppUpdateStatus.NotPackaged or AppUpdateStatus.Failed)
            {
                await MessageDialog.AlertAsync(this, UpdatePromptCopy.AvailableTitle, result.MessageKo).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"업데이트를 확인하지 못했습니다: {ex.Message}";
            await MessageDialog.AlertAsync(this, UpdatePromptCopy.AvailableTitle, UpdateStatusText.Text).ConfigureAwait(true);
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private void ShowIdleSearch()
    {
        var all = _indexing.GetIndexedDocuments();
        var coverage = CoverageOf(all);
        IndexedSummaryText.Text = SearchStatusFormatter.CoverageChip(coverage);
        IdleHintText.Text = SearchIdleCopy.Hint(coverage);
        ApplyFormatFilterVisuals();
        ShowIndexedFolder();
        SearchResultsList.ItemsSource = null;
        ApplySearchListMode(SearchListMode.Idle);
    }

    private void ShowIndexedFolder()
    {
        var folder = LoadSettings().IndexFolder ?? string.Empty;
        IndexedFolderPathText.Text = folder;
        IndexedFolderButton.IsVisible = !string.IsNullOrWhiteSpace(folder);
        ToolTip.SetTip(IndexedFolderButton, string.IsNullOrWhiteSpace(folder) ? null : folder);
    }

    private void ApplySearchListMode(SearchListMode mode)
    {
        IdleHintPanel.IsVisible = mode == SearchListMode.Idle;
        SearchResultsList.IsVisible = mode == SearchListMode.Hits;
        EmptyHintPanel.IsVisible = mode == SearchListMode.Empty;
        ResultCountText.IsVisible = mode != SearchListMode.Idle;
    }

    private void RunSearch()
    {
        var query = SearchQueryBox.Text;
        _searchSubmitted = !string.IsNullOrWhiteSpace(query);
        if (SearchListModeResolver.Resolve(query, _searchSubmitted, hitCount: 1) == SearchListMode.Idle)
        {
            ShowIdleSearch();
            return;
        }

        var all = _indexing.GetIndexedDocuments();
        var tokens = FilenameSearchQuery.ExtractTokens(query);
        var rows = _indexing.Search(query!, _formatFilter).Select(hit =>
        {
            var path = hit.Document.FilePath;
            var fileName = Path.GetFileName(path);
            var group = SearchResultDisplay.KindGroup(path);
            return new SearchResultRow
            {
                FileName = fileName,
                FilePath = path,
                KindLabel = IndexableFiles.Badge(path),
                Snippet = hit.Snippet,
                MatchLabel = hit.MatchLabelKo,
                HasSnippet = !string.IsNullOrWhiteSpace(hit.Snippet),
                LocationLine = SearchResultDisplay.LocationLine(path, hit.Document.LastWriteTimeUtc),
                IsPdf = group == SearchFormatFilter.Pdf,
                IsWord = group == SearchFormatFilter.Word,
                IsHangul = group == SearchFormatFilter.Hangul,
                IsExcel = group == SearchFormatFilter.Excel,
                FileNameSpans = EvidenceSnippet.Highlight(fileName, tokens),
                SnippetSpans = EvidenceSnippet.Highlight(hit.Snippet, tokens),
            };
        }).ToList();

        IndexedSummaryText.Text = FormatCoverage(all);
        ShowIndexedFolder();
        ResultCountText.Text = SearchStatusFormatter.HitCount(rows.Count);
        SearchResultsList.ItemsSource = rows;

        var mode = SearchListModeResolver.Resolve(query, _searchSubmitted, rows.Count);
        if (mode == SearchListMode.Hits)
        {
            ApplySearchListMode(SearchListMode.Hits);
            return;
        }

        var coverage = _indexing.GetCoverage();
        SearchEmptyText.Text = SearchStatusFormatter.EmptyResults(all.Count, coverage.BodyCount, _isIndexing, _formatFilter);
        ApplySearchListMode(SearchListMode.Empty);
    }

    private static string FormatCoverage(IReadOnlyList<IndexedDocument> all) =>
        SearchStatusFormatter.CoverageChip(CoverageOf(all));

    private static IndexCoverage CoverageOf(IReadOnlyList<IndexedDocument> all) =>
        new(
            all.Count,
            all.Count(doc => !string.IsNullOrWhiteSpace(doc.BodyText)),
            all.Sum(doc => doc.OcrPageCount),
            CompositeOcrEngine.CreateDefault().IsAvailable);

    private async Task TryResumeOrBackfillIndexingAsync()
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
        var resume = IndexResumePolicy.ShouldResume(settings);
        var backfill = IndexBackfillPolicy.ShouldBackfill(_indexing.GetCoverage(), settings.IndexFolder);
        var folder = settings.IndexFolder;
        var plan = !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
            ? _indexing.PlanSync(folder)
            : new IndexSyncPlan(0, 0, 0);
        var sync = !resume && !backfill && IndexSyncPolicy.ShouldAutoSync(settings, plan);
        if (!resume && !backfill && !sync)
        {
            if (settings.IndexingInProgress)
            {
                settings.IndexingInProgress = false;
                SaveSettings(settings);
            }

            if (SearchPanel.IsVisible)
            {
                if (_searchSubmitted)
                {
                    RunSearch();
                }
                else
                {
                    ShowIdleSearch();
                }
            }

            return;
        }

        if (sync && !string.IsNullOrWhiteSpace(folder))
        {
            await RunIndexingPassAsync(folder, IndexPass.NewAndChanged, "새로 넣은 파일만 읽는 중…").ConfigureAwait(true);
            return;
        }

        folder = settings.IndexFolder!;
        _isIndexing = true;
        UpdateIndexButtonState();
        BeginIndexingCancellation();
        MarkIndexingStarted();
        IndexedSummaryText.Text = resume ? "이어서 읽는 중…" : "본문 읽는 중…";
        IndexStatusText.Text = resume ? "업데이트가 끝난 뒤 인덱싱을 이어서 합니다." : "인덱싱 중…";
        if (SearchPanel.IsVisible && _searchSubmitted)
        {
            RunSearch();
        }
        else if (SearchPanel.IsVisible)
        {
            SearchResultsList.ItemsSource = null;
            ApplySearchListMode(SearchListMode.Idle);
        }

        var progress = new Progress<IndexingProgress>(snapshot =>
        {
            IndexedSummaryText.Text = resume
                ? SearchStatusFormatter.ResumeProgress(snapshot.ProcessedCount, snapshot.FoundCount)
                : SearchStatusFormatter.CoverageProgress(snapshot.ProcessedCount, snapshot.FoundCount);
            IndexCountText.Text = $"건수: {snapshot.ProcessedCount} / {snapshot.FoundCount}";
            IndexCurrentFileText.Text = string.IsNullOrWhiteSpace(snapshot.CurrentFile)
                ? "현재 파일: —"
                : $"현재 파일: {Path.GetFileName(snapshot.CurrentFile)}";
            IndexStatusText.Text = snapshot.IsCompleted
                ? FormatCompleteStatus(snapshot.Errors.Count)
                : resume
                    ? "이어서 읽는 중…"
                    : string.IsNullOrWhiteSpace(snapshot.PhaseKo) ? "인덱싱 중…" : snapshot.PhaseKo;
        });

        try
        {
            var indexing = _indexing.Start(folder, progress, IndexingToken());
            _indexingTask = indexing;
            await indexing.ConfigureAwait(true);
            settings = LoadSettings();
            IndexingRunState.OnFinished(settings, completed: true);
            SaveSettings(settings);
            _showMainSearch = true;
            if (SearchTab.IsChecked == true)
            {
                ShowMainSearch(resetSearch: false);
            }
        }
        catch (OperationCanceledException)
        {
            IndexedSummaryText.Text = _resumeIndexingAfterUpdate
                ? "업데이트가 끝나면 인덱싱을 이어서 합니다."
                : "인덱싱을 잠시 멈췄습니다.";
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"본문을 읽지 못했습니다: {ex.Message}";
        }
        finally
        {
            _indexingTask = null;
            _isIndexing = false;
            UpdateIndexButtonState();
            RefreshFolderWatch();
        }
    }

    private void BeginIndexingCancellation()
    {
        _indexCts?.Dispose();
        _indexCts = new CancellationTokenSource();
    }

    private CancellationToken IndexingToken() => _indexCts?.Token ?? CancellationToken.None;

    private void MarkIndexingStarted()
    {
        var settings = LoadSettings();
        IndexingRunState.OnStarted(settings);
        SaveSettings(settings);
    }

    private void RefreshFolderWatch()
    {
        var settings = LoadSettings();
        if (IndexWatchPolicy.ShouldWatchFolder(settings))
        {
            _folderWatch.SetFolder(settings.IndexFolder);
            return;
        }

        _folderWatch.Stop();
    }

    private async Task TryWatchSyncAsync()
    {
        if (_isIndexing)
        {
            _pendingWatchSync = true;
            return;
        }

        var settings = LoadSettings();
        if (!IndexWatchPolicy.ShouldWatchFolder(settings) || string.IsNullOrWhiteSpace(settings.IndexFolder))
        {
            return;
        }

        var folder = settings.IndexFolder;
        var plan = _indexing.PlanSync(folder);
        if (!plan.NeedsWork)
        {
            _watchRetryCount = 0;
            return;
        }

        await RunIndexingPassAsync(folder, IndexPass.NewAndChanged, "새로 넣은 파일만 읽는 중…", preserveSearch: true).ConfigureAwait(true);
        if (_indexing.PlanSync(folder).NeedsWork && _watchRetryCount < 3)
        {
            _watchRetryCount++;
            _folderWatch.Ping();
            return;
        }

        _watchRetryCount = 0;
    }

    private void SearchResultsList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is SearchResultRow row)
        {
            OpenIndexedFile(row.FilePath);
        }
    }

    private void OpenResultButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SearchResultRow row })
        {
            OpenIndexedFile(row.FilePath);
        }

        e.Handled = true;
    }

    private void RevealResultButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SearchResultRow row })
        {
            RevealIndexedFile(row.FilePath);
        }

        e.Handled = true;
    }

    private void OpenIndexedFile(string path)
    {
        if (!File.Exists(path))
        {
            ShowFileActionError("파일을 열 수 없습니다", "파일이 없거나 옮겨졌습니다. 제목 아래 폴더 경로를 확인해 보세요.");
            return;
        }

        try
        {
            Process.Start(LocalFileActions.Open(path));
        }
        catch (Exception ex)
        {
            ShowFileActionError("파일을 열 수 없습니다", ex.Message);
        }
    }

    private void RevealIndexedFile(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(Path.GetDirectoryName(path) ?? string.Empty))
        {
            ShowFileActionError("폴더를 열 수 없습니다", "파일이 없거나 옮겨졌습니다. 제목 아래 폴더 경로를 확인해 보세요.");
            return;
        }

        try
        {
            Process.Start(LocalFileActions.Reveal(path));
        }
        catch (Exception ex)
        {
            ShowFileActionError("폴더를 열 수 없습니다", ex.Message);
        }
    }

    private void ShowFileActionError(string title, string body) =>
        _ = MessageDialog.AlertAsync(this, title, body);

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
        public string KindLabel { get; init; } = "파일";
        public string Snippet { get; init; } = string.Empty;
        public string MatchLabel { get; init; } = string.Empty;
        public bool HasSnippet { get; init; }
        public string LocationLine { get; init; } = string.Empty;
        public bool IsPdf { get; init; }
        public bool IsWord { get; init; }
        public bool IsHangul { get; init; }
        public bool IsExcel { get; init; }
        public IReadOnlyList<SnippetSpan> FileNameSpans { get; init; } = [];
        public IReadOnlyList<SnippetSpan> SnippetSpans { get; init; } = [];
    }

    private sealed class HistoryRow
    {
        public required string Version { get; init; }
        public required string SummaryKo { get; init; }
        public bool IsCurrent { get; init; }
        public bool IsLast { get; init; }
    }
}
