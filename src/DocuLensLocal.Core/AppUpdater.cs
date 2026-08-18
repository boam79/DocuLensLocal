namespace DocuLensLocal.Core;

public enum AppUpdateStatus
{
    UpToDate,
    Applied,
    NotPackaged,
    Failed,
}

public sealed record AppUpdateCheckResult(AppUpdateStatus Status, string MessageKo);

public interface IUpdateFeed
{
    bool CanApplyUpdates { get; }

    Task<string?> FindNewerVersionAsync(CancellationToken cancellationToken);

    Task ApplyAsync(CancellationToken cancellationToken);
}

public sealed class AppUpdater
{
    public async Task<AppUpdateCheckResult> CheckAndApplyAsync(
        IUpdateFeed feed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feed);

        try
        {
            var newer = await feed.FindNewerVersionAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(newer))
            {
                return new AppUpdateCheckResult(AppUpdateStatus.UpToDate, "최신 버전입니다.");
            }

            if (!feed.CanApplyUpdates)
            {
                return new AppUpdateCheckResult(
                    AppUpdateStatus.NotPackaged,
                    $"새 버전 {newer}이(가) 있습니다. 설치본에서만 업데이트를 적용할 수 있습니다.");
            }

            await feed.ApplyAsync(cancellationToken).ConfigureAwait(false);
            return new AppUpdateCheckResult(
                AppUpdateStatus.Applied,
                $"버전 {newer}을(를) 적용하고 프로그램을 다시 시작합니다.");
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.Failed,
                $"업데이트를 확인하지 못했습니다: {ex.Message}");
        }
    }
}
