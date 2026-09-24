namespace DocuLensLocal.Core;

public static class UpdateNotesPolicy
{
    public static string? StartupNotes(string? pendingNotes, string? lastRunVersion, string currentVersion)
    {
        if (!string.IsNullOrWhiteSpace(pendingNotes))
        {
            return pendingNotes.Trim();
        }

        if (string.IsNullOrWhiteSpace(currentVersion)
            || string.IsNullOrWhiteSpace(lastRunVersion)
            || string.Equals(lastRunVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var items = ReleaseHistory.Between(lastRunVersion, currentVersion);
        return items.Count == 0 ? null : ReleaseHistory.FormatNotes(lastRunVersion, currentVersion);
    }

    public static string AvailablePrompt(string currentVersion, string newerVersion, bool indexingNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newerVersion);
        return UpdatePromptCopy.AvailableBody(
            newerVersion,
            indexingNow,
            ReleaseHistory.FormatNotes(currentVersion, newerVersion));
    }
}
