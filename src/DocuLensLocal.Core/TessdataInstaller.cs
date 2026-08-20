namespace DocuLensLocal.Core;

public sealed class TessdataInstaller
{
    public static readonly Uri EngUrl = new("https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata");
    public static readonly Uri KorUrl = new("https://github.com/tesseract-ocr/tessdata_fast/raw/main/kor.traineddata");

    private readonly HttpMessageHandler? _handler;

    public TessdataInstaller()
    {
    }

    public TessdataInstaller(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public static Task<string> EnsureUserDataAsync(CancellationToken cancellationToken = default)
    {
        var existing = TessdataLocator.FindDirectory();
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        return new TessdataInstaller().EnsureAsync(TessdataLocator.UserDataDirectory, cancellationToken);
    }

    public async Task<string> EnsureAsync(string directory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        await DownloadIfMissing(directory, TessdataLocator.EngFileName, EngUrl, cancellationToken).ConfigureAwait(false);
        await DownloadIfMissing(directory, TessdataLocator.KorFileName, KorUrl, cancellationToken).ConfigureAwait(false);
        return directory;
    }

    private async Task DownloadIfMissing(string directory, string fileName, Uri url, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, fileName);
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            return;
        }

        using var client = CreateClient();
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    private HttpClient CreateClient()
    {
        var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromMinutes(2);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DocuLensLocal/0.1.10");
        return client;
    }
}
