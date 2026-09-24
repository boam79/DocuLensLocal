using System.Diagnostics;

namespace DocuLensLocal.Core;

public enum LocalFileOs
{
    Windows,
    Mac,
    Linux,
}

public static class LocalFileActions
{
    public static ProcessStartInfo Open(string path) =>
        new(path) { UseShellExecute = true };

    public static ProcessStartInfo Reveal(string path) =>
        Reveal(path, CurrentOs());

    public static ProcessStartInfo Reveal(string path, LocalFileOs os)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return os switch
        {
            LocalFileOs.Windows => new ProcessStartInfo
            {
                FileName = "explorer",
                Arguments = "/select,\"" + path.Replace('/', '\\') + "\"",
                UseShellExecute = true,
            },
            LocalFileOs.Mac => MacReveal(path),
            _ => LinuxReveal(path),
        };
    }

    public static LocalFileOs CurrentOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return LocalFileOs.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return LocalFileOs.Mac;
        }

        return LocalFileOs.Linux;
    }

    private static ProcessStartInfo MacReveal(string path)
    {
        var info = new ProcessStartInfo
        {
            FileName = "open",
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-R");
        info.ArgumentList.Add(path);
        return info;
    }

    private static ProcessStartInfo LinuxReveal(string path)
    {
        var info = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false,
        };
        info.ArgumentList.Add(Path.GetDirectoryName(path) ?? path);
        return info;
    }
}
