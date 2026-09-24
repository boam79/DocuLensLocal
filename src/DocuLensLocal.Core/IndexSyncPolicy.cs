namespace DocuLensLocal.Core;

public static class IndexSyncPolicy
{
    public static bool ShouldAutoSync(AppSettings settings, IndexSyncPlan plan)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(plan);
        return settings.IndexCompleted
            && !settings.IndexingInProgress
            && plan.NeedsWork
            && IndexFolderList.AnyExists(IndexFolderList.FromSettings(settings));
    }
}
