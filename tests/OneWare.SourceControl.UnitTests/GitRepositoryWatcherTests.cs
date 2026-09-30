using LibGit2Sharp;
using Xunit;

namespace OneWare.SourceControl.UnitTests;

public sealed class GitRepositoryWatcherTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(1500);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OneWare.Git.Tests", Guid.NewGuid().ToString("N"));
    private readonly Repository _repository;
    private readonly Signature _signature = new("Git Tests", "git-tests@example.invalid", DateTimeOffset.Now);

    public GitRepositoryWatcherTests()
    {
        Repository.Init(_directory);
        _repository = new Repository(_directory);
        _repository.Config.Set("core.autocrlf", false);
        _repository.Config.Set("commit.gpgsign", false);
        File.WriteAllText(Path.Combine(_directory, "file.txt"), "base\n");
        Commands.Stage(_repository, "file.txt");
        _repository.Commit("base", _signature, _signature);
    }

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_directory, true);
        }
        catch
        {
            // Best effort cleanup of the temporary repository.
        }
    }

    private GitRepositoryWatcher CreateWatcher(SemaphoreSlim signal)
    {
        var watcher = new GitRepositoryWatcher(_repository.Info.WorkingDirectory, _repository.Info.Path,
            () => signal.Release());
        Assert.True(watcher.IsActive);
        return watcher;
    }

    [Fact]
    public async Task ReportsWorkingTreeChanges()
    {
        using var signal = new SemaphoreSlim(0);
        using var watcher = CreateWatcher(signal);

        await File.WriteAllTextAsync(Path.Combine(_directory, "file.txt"), "changed\n");

        Assert.True(await signal.WaitAsync(Timeout));
    }

    [Fact]
    public async Task ReportsExternalStagingAndCommits()
    {
        using var signal = new SemaphoreSlim(0);
        using var watcher = CreateWatcher(signal);

        using (var external = new Repository(_directory))
        {
            await File.WriteAllTextAsync(Path.Combine(_directory, "file.txt"), "changed\n");
            Commands.Stage(external, "file.txt");
            external.Commit("next", _signature, _signature);
        }

        Assert.True(await signal.WaitAsync(Timeout));
    }

    [Fact]
    public async Task ReadingStatusDoesNotTriggerARefreshLoop()
    {
        using var signal = new SemaphoreSlim(0);
        using var watcher = CreateWatcher(signal);
        watcher.MarkRefreshed();

        for (var i = 0; i < 3; i++)
        {
            _ = _repository.RetrieveStatus(new StatusOptions()).ToArray();
            _ = _repository.Head.TrackingDetails.AheadBy;
            watcher.MarkRefreshed();
        }

        Assert.False(await signal.WaitAsync(Quiet));
    }

    [Fact]
    public async Task IgnoresObjectDatabaseAndLockFiles()
    {
        using var signal = new SemaphoreSlim(0);
        using var watcher = CreateWatcher(signal);

        var objects = Path.Combine(_repository.Info.Path, "objects", "ab");
        Directory.CreateDirectory(objects);
        await File.WriteAllTextAsync(Path.Combine(objects, "cdef"), "x");
        await File.WriteAllTextAsync(Path.Combine(_repository.Info.Path, "index.lock"), "x");
        File.Delete(Path.Combine(_repository.Info.Path, "index.lock"));

        Assert.False(await signal.WaitAsync(Quiet));
    }

    [Fact]
    public async Task BurstsAreDebouncedIntoOneNotification()
    {
        using var signal = new SemaphoreSlim(0);
        using var watcher = CreateWatcher(signal);

        for (var i = 0; i < 20; i++)
            await File.WriteAllTextAsync(Path.Combine(_directory, $"burst{i}.txt"), "x");

        Assert.True(await signal.WaitAsync(Timeout));
        Assert.False(await signal.WaitAsync(Quiet));
    }
}
