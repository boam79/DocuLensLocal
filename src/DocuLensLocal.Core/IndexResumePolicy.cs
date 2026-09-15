namespace DocuLensLocal.Core;

public static class IndexResumePolicy
{
    public static bool ShouldResume(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.IndexingInProgress
            && IndexFolderList.AnyExists(IndexFolderList.FromSettings(settings));
    }
}

public static class IndexingRunState
{
    public static void OnStarted(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.IndexingInProgress = true;
    }

    public static void OnFinished(AppSettings settings, bool completed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.IndexingInProgress = false;
        settings.IndexCompleted = completed;
    }
}
