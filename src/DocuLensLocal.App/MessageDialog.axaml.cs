using Avalonia.Controls;
using Avalonia.Interactivity;
using DocuLensLocal.Core;

namespace DocuLensLocal.App;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public static Task<bool> ConfirmAsync(Window owner, string title, string body) =>
        ConfirmAsync(owner, title, body, UpdatePromptCopy.Confirm, UpdatePromptCopy.Later);

    public static Task<bool> ConfirmAsync(
        Window owner,
        string title,
        string body,
        string primary,
        string secondary) =>
        ShowAsync(owner, title, body, primary, secondary, showSecondary: true);

    public static async Task AlertAsync(Window owner, string title, string body)
    {
        await ShowAsync(owner, title, body, UpdatePromptCopy.NotesOk, secondary: null, showSecondary: false).ConfigureAwait(true);
    }

    private static async Task<bool> ShowAsync(
        Window owner,
        string title,
        string body,
        string primary,
        string? secondary,
        bool showSecondary)
    {
        var dialog = new MessageDialog
        {
            Title = title,
        };
        dialog.TitleText.Text = title;
        dialog.BodyText.Text = body;
        dialog.PrimaryButton.Content = primary;
        dialog.SecondaryButton.Content = secondary ?? string.Empty;
        dialog.SecondaryButton.IsVisible = showSecondary;
        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private void PrimaryButton_OnClick(object? sender, RoutedEventArgs e) => Close(true);

    private void SecondaryButton_OnClick(object? sender, RoutedEventArgs e) => Close(false);
}
