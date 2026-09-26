using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Core.Services;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public sealed class BinaryCacheServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oneware-binary-cache-tests", Guid.NewGuid().ToString("N"));
    private readonly string _cache;
    private readonly string _source;

    public BinaryCacheServiceTests()
    {
        _cache = Path.Combine(_root, "cache");
        _source = Path.Combine(_root, "source", "MyPlugin");
        Directory.CreateDirectory(Path.Combine(_source, "runtimes"));
        File.WriteAllText(Path.Combine(_source, "plugin.dll"), "v1");
        File.WriteAllText(Path.Combine(_source, "runtimes", "native.so"), "native");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private BinaryCacheService CreateService() => new(_cache, NullLogger.Instance);

    [Fact]
    public void CopyIsCreatedOnceAndReused()
    {
        string? first;
        using (var service = CreateService())
        {
            first = service.GetOrCreateCopy("Plugins", "MyPlugin", _source);
            Assert.NotNull(first);
            Assert.Equal("MyPlugin", Path.GetFileName(first));
            Assert.Equal("v1", File.ReadAllText(Path.Combine(first, "plugin.dll")));
            Assert.True(File.Exists(Path.Combine(first, "runtimes", "native.so")));
        }

        var marker = Path.Combine(first!, "plugin.dll");
        var writeTime = File.GetLastWriteTimeUtc(marker);

        using (var service = CreateService())
        {
            var second = service.GetOrCreateCopy("Plugins", "MyPlugin", _source);
            Assert.Equal(first, second);
            Assert.Equal(writeTime, File.GetLastWriteTimeUtc(marker));
        }
    }

    [Fact]
    public void ChangedSourceCreatesNewEntryAndCleanupRemovesOldOne()
    {
        using var oldService = CreateService();
        var first = oldService.GetOrCreateCopy("Plugins", "MyPlugin", _source)!;

        File.WriteAllText(Path.Combine(_source, "plugin.dll"), "version 2");

        using var newService = CreateService();
        var second = newService.GetOrCreateCopy("Plugins", "MyPlugin", _source)!;

        Assert.NotEqual(first, second);
        Assert.Equal("version 2", File.ReadAllText(Path.Combine(second, "plugin.dll")));

        // The old entry is still in use by the first instance
        newService.CleanupUnusedEntries();
        Assert.True(Directory.Exists(first));

        oldService.Dispose();
        newService.CleanupUnusedEntries();

        Assert.False(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(Path.GetDirectoryName(second))!));
    }

    [Fact]
    public void CleanupKeepsUpToDateEntriesAndRemovesLeftovers()
    {
        string copy;
        using (var service = CreateService())
        {
            copy = service.GetOrCreateCopy("Plugins", "MyPlugin", _source)!;
        }

        var nameDirectory = Path.GetDirectoryName(Path.GetDirectoryName(copy))!;
        var leftover = Path.Combine(nameDirectory, ".deleting-abc");
        Directory.CreateDirectory(leftover);
        var incomplete = Path.Combine(nameDirectory, "0123456789abcdef");
        Directory.CreateDirectory(incomplete);

        using (var service = CreateService())
        {
            service.CleanupUnusedEntries();
        }

        Assert.True(Directory.Exists(copy));
        Assert.False(Directory.Exists(leftover));
        Assert.False(Directory.Exists(incomplete));
    }

    [Fact]
    public void RemovedSourceEntryIsCleanedUp()
    {
        string copy;
        using (var service = CreateService())
        {
            copy = service.GetOrCreateCopy("Plugins", "MyPlugin", _source)!;
        }

        Directory.Delete(_source, true);

        using (var service = CreateService())
        {
            service.CleanupUnusedEntries();
        }

        Assert.False(Directory.Exists(copy));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_cache, "Plugins")));
    }

    [Fact]
    public void MissingSourceReturnsNull()
    {
        using var service = CreateService();
        Assert.Null(service.GetOrCreateCopy("Plugins", "Missing", Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public void StampChangesWhenFilesChange()
    {
        var before = BinaryCacheService.ComputeStamp(_source);
        Assert.Equal(before, BinaryCacheService.ComputeStamp(_source));

        File.WriteAllText(Path.Combine(_source, "extra.dll"), "x");
        Assert.NotEqual(before, BinaryCacheService.ComputeStamp(_source));
    }
}
