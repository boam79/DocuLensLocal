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
    private CancellationTokenSource? _indexCts;
    private Task? _indexingTask;
    private bool _isIndexing;
    private bool _showMainSearch;
    private bool _searchSubmitted;
    private bool _resumeIndexingAfterUpdate;

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
            await TryShowPendingUpdateNotesAsync().ConfigureAwait(true);
            await TryPromptForUpdateAsync().ConfigureAwait(true);
            await TryResumeOrBackfillIndexingAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            IndexedSummaryText.Text = $"시작하지 못했습니다: {ex.Message}";
        }
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
        ChangeFolderButton.IsEnabled = !_isIndexing;
        SyncIndexButton.IsEnabled = !_isIndexing
            && !string.IsNullOrWhiteSpace(folder)
            && Directory.Exists(folder);
        RebuildIndexButton.IsEnabled = !_isIndexing
            && !string.IsNullOrWhiteSpace(folder)
            && Directory.Exists(folder);
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
        IndexStatusText.Text = "폴더를 바꾼 뒤 인덱싱을 누르면 그 폴더로 목록을 맞춥니다. 폴더만 고르면 인덱싱은 시작하지 않습니다.";
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

    private async Task RunIndexingPassAsync(string folder, IndexPass pass, string startMessage)
    {
        if (_isIndexing)
        {
            return;
        }

        _isIndexing = true;
        UpdateIndexButtonState();
        _searchSubmitted = false;
        SearchQueryBox.Text = string.Empty;
        SearchResultsList.ItemsSource = null;
        ApplySearchListMode(SearchListMode.Idle);
        IndexedSummaryText.Text = startMessage;
        IdleHintText.Text = pass == IndexPass.NewAndChanged
            ? "이미 읽은 파일은 그대로 두고, 새로 넣거나 바뀐 파일만 읽습니다."
            : "파일명이나 본문 단어로 찾아 보세요";
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
                ShowMainSearch(resetSearch: true);
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
        IndexedSummaryText.Text = SearchStatusFormatter.Coverage(coverage);
        IdleHintText.Text = SearchIdleCopy.Hint(coverage);
        IndexedFolderText.Text = LoadSettings().IndexFolder ?? string.Empty;
        SearchResultsList.ItemsSource = null;
        ApplySearchListMode(SearchListMode.Idle);
    }

    private void ApplySearchListMode(SearchListMode mode)
    {
        IdleHintPanel.IsVisible = mode == SearchListMode.Idle;
        SearchResultsList.IsVisible = mode == SearchListMode.Hits;
        EmptyHintPanel.IsVisible = mode == SearchListMode.Empty;
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
        var rows = _indexing.Search(query!).Select(hit => new SearchResultRow
        {
            FileName = Path.GetFileName(hit.Document.FilePath),
            FilePath = hit.Document.FilePath,
            KindLabel = IndexableFiles.Badge(hit.Document.FilePath),
            Snippet = hit.Snippet,
            MatchLabel = hit.MatchLabelKo,
            HasSnippet = !string.IsNullOrWhiteSpace(hit.Snippet),
        }).ToList();

        IndexedSummaryText.Text = FormatCoverage(all);
        IndexedFolderText.Text = LoadSettings().IndexFolder ?? string.Empty;
        SearchResultsList.ItemsSource = rows;

        var mode = SearchListModeResolver.Resolve(query, _searchSubmitted, rows.Count);
        if (mode == SearchListMode.Hits)
        {
            ApplySearchListMode(SearchListMode.Hits);
            return;
        }

        var coverage = _indexing.GetCoverage();
        SearchEmptyText.Text = SearchStatusFormatter.EmptyResults(all.Count, coverage.BodyCount, _isIndexing);
        ApplySearchListMode(SearchListMode.Empty);
    }

    private static string FormatCoverage(IReadOnlyList<IndexedDocument> all) =>
        SearchStatusFormatter.Coverage(CoverageOf(all));

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
            ApplySearchListMode(SearchListMode.Empty);
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
        public string KindLabel { get; init; } = "파일";
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
