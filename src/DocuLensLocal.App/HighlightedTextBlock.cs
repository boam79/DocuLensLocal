using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using DocuLensLocal.Core;

namespace DocuLensLocal.App;

public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly StyledProperty<IReadOnlyList<SnippetSpan>?> SpansProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, IReadOnlyList<SnippetSpan>?>(nameof(Spans));

    private static readonly IBrush HitBackground = new SolidColorBrush(Color.Parse("#CCFBF1"));
    private static readonly IBrush HitForeground = new SolidColorBrush(Color.Parse("#0F766E"));

    public IReadOnlyList<SnippetSpan>? Spans
    {
        get => GetValue(SpansProperty);
        set => SetValue(SpansProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SpansProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var inlines = Inlines;
        if (inlines is null)
        {
            return;
        }

        inlines.Clear();
        if (Spans is null || Spans.Count == 0)
        {
            return;
        }

        foreach (var span in Spans)
        {
            if (span.Text.Length == 0)
            {
                continue;
            }

            var run = new Run(span.Text);
            if (span.IsHit)
            {
                run.FontWeight = FontWeight.Bold;
                run.Background = HitBackground;
                run.Foreground = HitForeground;
            }

            inlines.Add(run);
        }
    }
}
