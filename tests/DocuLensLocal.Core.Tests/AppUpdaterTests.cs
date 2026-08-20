using DocuLensLocal.Core;

namespace DocuLensLocal.Core.Tests;

public class AppUpdaterTests
{
    private readonly AppUpdater _updater = new();

    [Fact]
    public async Task no_update_returns_korean_latest_message()
    {
        var feed = new FakeUpdateFeed { NewerVersion = null, CanApplyUpdates = true };

        var result = await _updater.CheckAndApplyAsync(feed);

        Assert.Equal(AppUpdateStatus.UpToDate, result.Status);
        Assert.Equal("최신 버전입니다.", result.MessageKo);
        Assert.False(feed.ApplyCalled);
    }

    [Fact]
    public async Task packaged_app_downloads_and_applies_found_update()
    {
        var feed = new FakeUpdateFeed { NewerVersion = "0.1.4", CanApplyUpdates = true };

        var result = await _updater.CheckAndApplyAsync(feed);

        Assert.Equal(AppUpdateStatus.Applied, result.Status);
        Assert.Contains("0.1.4", result.MessageKo);
        Assert.True(feed.ApplyCalled);
    }

    [Fact]
    public async Task unpackaged_debug_build_does_not_fake_success()
    {
        var feed = new FakeUpdateFeed { NewerVersion = "0.1.4", CanApplyUpdates = false };

        var result = await _updater.CheckAndApplyAsync(feed);

        Assert.Equal(AppUpdateStatus.NotPackaged, result.Status);
        Assert.Contains("0.1.4", result.MessageKo);
        Assert.DoesNotContain("최신 버전입니다.", result.MessageKo);
        Assert.False(feed.ApplyCalled);
    }

    [Fact]
    public async Task check_failure_returns_korean_error_not_success()
    {
        var feed = new FakeUpdateFeed { ThrowOnCheck = new InvalidOperationException("network down") };

        var result = await _updater.CheckAndApplyAsync(feed);

        Assert.Equal(AppUpdateStatus.Failed, result.Status);
        Assert.Contains("업데이트를 확인하지 못했습니다", result.MessageKo);
        Assert.Contains("network down", result.MessageKo);
        Assert.False(feed.ApplyCalled);
    }

    [Fact]
    public async Task check_async_does_not_apply_when_update_exists()
    {
        var feed = new FakeUpdateFeed { NewerVersion = "0.1.16", CanApplyUpdates = true };

        var result = await _updater.CheckAsync(feed);

        Assert.Equal(AppUpdateStatus.Available, result.Status);
        Assert.Equal("0.1.16", result.NewerVersion);
        Assert.Contains("0.1.16", result.MessageKo, StringComparison.Ordinal);
        Assert.False(feed.ApplyCalled);
    }

    [Fact]
    public async Task apply_async_downloads_after_user_confirms()
    {
        var feed = new FakeUpdateFeed { NewerVersion = "0.1.16", CanApplyUpdates = true };

        var result = await _updater.ApplyAsync(feed, "0.1.16");

        Assert.Equal(AppUpdateStatus.Applied, result.Status);
        Assert.True(feed.ApplyCalled);
    }

    [Fact]
    public void update_prompt_copy_is_plain_korean()
    {
        Assert.Equal("업데이트가 있습니다", UpdatePromptCopy.AvailableTitle);
        Assert.Contains("확인을 누르면", UpdatePromptCopy.AvailableBody("0.1.16"), StringComparison.Ordinal);
        Assert.DoesNotContain("이어서", UpdatePromptCopy.AvailableBody("0.1.16"), StringComparison.Ordinal);
        Assert.Contains("이어서", UpdatePromptCopy.AvailableBody("0.1.16", indexingNow: true), StringComparison.Ordinal);
        Assert.Equal("업데이트 내역", UpdatePromptCopy.NotesTitle);
    }

    private sealed class FakeUpdateFeed : IUpdateFeed
    {
        public string? NewerVersion { get; init; }
        public bool CanApplyUpdates { get; init; }
        public Exception? ThrowOnCheck { get; init; }
        public bool ApplyCalled { get; private set; }

        public Task<string?> FindNewerVersionAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnCheck is not null)
            {
                throw ThrowOnCheck;
            }

            return Task.FromResult(NewerVersion);
        }

        public Task ApplyAsync(CancellationToken cancellationToken)
        {
            ApplyCalled = true;
            return Task.CompletedTask;
        }
    }
}
