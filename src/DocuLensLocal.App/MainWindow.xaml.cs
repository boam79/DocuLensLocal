using System.IO;
using System.Text.Json;
using System.Windows;
using DocuLensLocal.Core;
using Microsoft.Win32;

namespace DocuLensLocal.App;

public partial class MainWindow : Window
{
    private readonly IndexingService _indexing = new();
    private bool _isIndexing;

    public MainWindow()
    {
        InitializeComponent();
        ShowSavedFolder();
        UpdateIndexButtonState();
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

        Directory.CreateDirectory(AppPaths.UserData);
        var settings = new AppSettings { IndexFolder = dialog.FolderName };
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings));
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
        IndexCountText.Text = $"건수: {result.ProcessedCount} / {result.FoundCount}";
        IndexStatusText.Text = FormatCompleteStatus(result.Errors.Count);
        if (result.Documents.Count > 0)
        {
            IndexCurrentFileText.Text = $"현재 파일: {Path.GetFileName(result.Documents[^1].FilePath)}";
        }
        else if (result.IsCompleted)
        {
            IndexCurrentFileText.Text = "현재 파일: —";
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
}
