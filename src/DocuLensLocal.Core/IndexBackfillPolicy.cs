namespace DocuLensLocal.Core;

public static class IndexBackfillPolicy
{
    public static bool ShouldBackfill(IndexCoverage coverage, string? indexFolder) =>
        ShouldBackfill(coverage, IndexFolderList.Normalize(indexFolder));

    public static bool ShouldBackfill(IndexCoverage coverage, IReadOnlyList<string> folders) =>
        coverage.DocumentCount > 0
        && coverage.BodyCount == 0
        && IndexFolderList.AnyExists(folders);
}
