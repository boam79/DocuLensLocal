namespace DocuLensLocal.Core;

public sealed class CompositeOcrEngine : IOcrEngine
{
    private readonly IOcrEngine[] _engines;

    public CompositeOcrEngine(params IOcrEngine[] engines)
    {
        ArgumentNullException.ThrowIfNull(engines);
        _engines = engines;
    }

    public static CompositeOcrEngine CreateDefault() =>
        new(new TesseractLibraryOcrEngine(), new TesseractCliOcrEngine());

    public bool IsAvailable => _engines.Any(engine => engine.IsAvailable);

    public string RecognizePng(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        foreach (var engine in _engines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!engine.IsAvailable)
            {
                continue;
            }

            try
            {
                var text = engine.RecognizePng(pngBytes, cancellationToken);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
        }

        return string.Empty;
    }
}
