using Velopack;
using Velopack.Sources;
using DocuLensLocal.Core;

namespace DocuLensLocal.App;

public sealed class VelopackUpdateFeed : IUpdateFeed
{
    public const string GitHubRepoUrl = "https://github.com/boam79/DocuLensLocal";

    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    public VelopackUpdateFeed()
        : this(new UpdateManager(new GithubSource(GitHubRepoUrl, accessToken: null, prerelease: false)))
    {
    }

    public VelopackUpdateFeed(UpdateManager manager)
    {
        _manager = manager;
    }

    public bool CanApplyUpdates => _manager.IsInstalled;

    public async Task<string?> FindNewerVersionAsync(CancellationToken cancellationToken)
    {
        _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return _pending?.TargetFullRelease.Version.ToString();
    }

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var info = _pending ?? await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (info is null)
        {
            throw new InvalidOperationException("적용할 업데이트가 없습니다.");
        }

        await _manager.DownloadUpdatesAsync(info).ConfigureAwait(false);
        _manager.ApplyUpdatesAndRestart(info);
    }
}
