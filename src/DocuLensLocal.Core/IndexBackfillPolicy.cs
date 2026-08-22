namespace DocuLensLocal.Core;

public static class IndexBackfillPolicy
{
    public static bool ShouldBackfill(IndexCoverage coverage, string? indexFolder) =>
        coverage.DocumentCount > 0
        && coverage.BodyCount == 0
        && !string.IsNullOrWhiteSpace(indexFolder)
        && Directory.Exists(indexFolder);
}
