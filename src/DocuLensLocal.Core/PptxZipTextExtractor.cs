namespace DocuLensLocal.Core;

public static class PptxZipTextExtractor
{
    public static string Extract(string path, CancellationToken cancellationToken = default) =>
        ZipOfficeTextExtractor.Extract(path, IsSlideBodyXml, cancellationToken);

    public static bool IsSlideBodyXml(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return false;
        }

        var name = fullName.Replace('\\', '/');
        if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Contains("/notesSlides/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Contains("/slides/", StringComparison.OrdinalIgnoreCase);
    }
}
