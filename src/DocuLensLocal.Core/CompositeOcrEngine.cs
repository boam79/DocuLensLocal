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
        var engine = _engines.FirstOrDefault(item => item.IsAvailable);
        if (engine is null)
        {
            return string.Empty;
        }

        try
        {
            return engine.RecognizePng(pngBytes, cancellationToken) ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }
}
