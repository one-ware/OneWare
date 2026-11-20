using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneWare.Chat.Services;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class AiFileEditServiceTests
{
    private const string Original = "A\nB\nC\nD\nE";

    [Theory]
    // A trailing newline in the content is a terminator, not a separator.
    [InlineData(3, 1, "X\n", "A\nB\nX\nD\nE")]
    [InlineData(3, 1, "X", "A\nB\nX\nD\nE")]
    [InlineData(3, 0, "X\n", "A\nB\nX\nC\nD\nE")]
    [InlineData(3, 2, "X\nY\n", "A\nB\nX\nY\nE")]
    [InlineData(3, 1, "X\nY", "A\nB\nX\nY\nD\nE")]
    [InlineData(1, 1, "X\n", "X\nB\nC\nD\nE")]
    [InlineData(3, 1, "X\r\n", "A\nB\nX\nD\nE")]
    // Only a single trailing newline is dropped; extra ones are intentional blank lines.
    [InlineData(3, 1, "X\n\n", "A\nB\nX\n\nD\nE")]
    [InlineData(3, 1, "\n", "A\nB\n\nD\nE")]
    // Deletion without replacement content.
    [InlineData(3, 1, "", "A\nB\nD\nE")]
    public void ApplyLineEdit_TreatsTrailingNewlineAsTerminator(int startLine, int lineCount, string content,
        string expected)
    {
        Assert.Equal(expected, AiFileEditService.ApplyLineEdit(Original, startLine, lineCount, content));
    }

    [Fact]
    public void ApplyLineEdit_IsIdempotentWhenRepeated()
    {
        var once = AiFileEditService.ApplyLineEdit(Original, 3, 1, "X\n");
        var twice = AiFileEditService.ApplyLineEdit(once, 3, 1, "X\n");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ApplyLineEdit_PreservesTrailingNewlineOfOriginal()
    {
        Assert.Equal("A\nB\nX\n", AiFileEditService.ApplyLineEdit("A\nB\nC\n", 3, 1, "X\n"));
    }

    [Fact]
    public void ApplyLineEdit_PreservesWindowsNewlinesOfOriginal()
    {
        Assert.Equal("A\r\nB\r\nX\r\nD", AiFileEditService.ApplyLineEdit("A\r\nB\r\nC\r\nD", 3, 1, "X\n"));
    }

    [Fact]
    public async Task EditFileAsync_ParallelLineEditsOnOneFile_AllApplyWithoutCorruption()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aiedit-{Guid.NewGuid():N}.txt");
        try
        {
            var lines = Enumerable.Range(1, 1000)
                .Select(i => $"Zeile {i}: Grüße äöü ß → – {new string('x', 100)}").ToArray();
            await File.WriteAllTextAsync(path, string.Join("\r\n", lines));

            var service = new AiFileEditService(FakeService<IMainDockService>.Create(),
                FakeService<ISettingsService>.Create());

            var results = await Task.WhenAll(Enumerable.Range(1, 10)
                .Select(i => Task.Run(() => service.EditFileAsync(path, $"E{i}", i * 100, 1, "chat"))));

            Assert.All(results, Assert.True);
            for (var i = 1; i <= 10; i++)
                lines[i * 100 - 1] = $"E{i}";

            var written = await File.ReadAllBytesAsync(path);
            Assert.DoesNotContain((byte)0, written);
            Assert.Equal(string.Join("\r\n", lines), Encoding.UTF8.GetString(written));
            Assert.Single(service.ActiveEdits);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteFileAtomicAsync_ReplacesContentAndKeepsFileMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aiedit-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(path, new string('a', 10_000));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            await AiFileEditService.WriteFileAtomicAsync(path, "short");

            Assert.Equal("short", await File.ReadAllTextAsync(path));
            if (!OperatingSystem.IsWindows())
                Assert.True(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private class FakeService<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, FakeService<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var type = targetMethod!.ReturnType;
            if (type == typeof(void)) return null;
            if (type == typeof(Dictionary<string, IExtendedDocument>)) return new Dictionary<string, IExtendedDocument>();
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
