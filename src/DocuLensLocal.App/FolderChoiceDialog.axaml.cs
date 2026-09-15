using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DocuLensLocal.App;

public partial class FolderChoiceDialog : Window
{
    public FolderChoiceDialog()
    {
        InitializeComponent();
    }

    public static async Task<string?> PickAsync(Window owner, IReadOnlyList<string> folders)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(folders);
        var dialog = new FolderChoiceDialog
        {
            Title = "빼 둘 폴더",
        };
        dialog.FolderList.ItemsSource = folders.ToList();
        if (folders.Count > 0)
        {
            dialog.FolderList.SelectedIndex = 0;
        }

        return await dialog.ShowDialog<string?>(owner).ConfigureAwait(true);
    }

    private void PrimaryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string path && !string.IsNullOrWhiteSpace(path))
        {
            Close(path);
            return;
        }

        Close(null);
    }

    private void SecondaryButton_OnClick(object? sender, RoutedEventArgs e) => Close(null);
}
