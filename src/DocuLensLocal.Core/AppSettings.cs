namespace DocuLensLocal.Core;

public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DocuLensLocal");

    public static string UserData => Path.Combine(Root, "userdata");

    public static string SettingsFile => Path.Combine(UserData, "settings.json");

    public static string IndexDatabase => Path.Combine(UserData, "index.db");
}

public sealed class AppSettings
{
    public string? IndexFolder { get; set; }

    /// <summary>True after IndexingService.Start completed, including 0-file folders.</summary>
    public bool IndexCompleted { get; set; }
}
