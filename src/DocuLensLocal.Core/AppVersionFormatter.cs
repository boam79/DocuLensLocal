namespace DocuLensLocal.Core;

public static class AppVersionFormatter
{
    public static string DisplayVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plus = informationalVersion.IndexOf('+');
            var core = plus >= 0 ? informationalVersion[..plus] : informationalVersion;
            return core.Trim();
        }

        if (assemblyVersion is not null)
        {
            return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        return "0.0.0";
    }
}
