namespace DocuLensLocal.Core;

public enum StartupView
{
    FirstRunFolderSelect,
    MainSearch,
}

public static class StartupViewResolver
{
    public static StartupView Resolve(AppSettings settings, int indexedDocumentCount)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (indexedDocumentCount > 0 || settings.IndexCompleted)
        {
            return StartupView.MainSearch;
        }

        return StartupView.FirstRunFolderSelect;
    }
}
