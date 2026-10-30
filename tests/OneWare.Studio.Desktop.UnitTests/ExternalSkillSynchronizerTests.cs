using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OneWare.Essentials.Services;
using OneWare.Studio.Desktop.Cli;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class ExternalSkillSynchronizerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "oneware-skills-test-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _output = new();
    private readonly StringWriter _errors = new();

    private string Documents => Path.Combine(_root, "documents");
    private string Packages => Path.Combine(Documents, "Packages");
    private string Plugins => Path.Combine(Packages, "Plugins");
    private string Plugin => Path.Combine(Plugins, "sample-extension");
    private string SourceRoot => Path.Combine(Plugin, "ExternalSkills");
    private string Source => Path.Combine(SourceRoot, "sample-skill");
    private string Destination => Path.Combine(_root, "home", ".agents", "skills", "sample-skill");
    private string Manifest => Path.Combine(Documents, "oneware-skills.json");

    public ExternalSkillSynchronizerTests()
    {
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, "SKILL.md"),
            "---\nname: sample-skill\ndescription: A sample skill\n---\nInstructions\n");
        Directory.CreateDirectory(Path.Combine(Source, "references"));
        File.WriteAllText(Path.Combine(Source, "references", "format.txt"), "reference");
        WritePackages("sample-extension", "1.0.0");
    }

    [Fact]
    public void InstallsUpdatesAndRemovesAnUninstalledExtensionsSkill()
    {
        Assert.Equal(0, Sync());
        Assert.Equal("reference", File.ReadAllText(Path.Combine(Destination, "references", "format.txt")));
        using (var json = JsonDocument.Parse(File.ReadAllText(Manifest)))
        {
            var entry = json.RootElement.GetProperty("Skills").GetProperty("sample-skill");
            Assert.Equal("package:sample-extension", entry.GetProperty("Source").GetString());
            Assert.Equal("1.0.0", entry.GetProperty("Version").GetString());
        }

        File.WriteAllText(Path.Combine(Source, "references", "format.txt"), "updated");
        WritePackages("sample-extension", "2.0.0");
        Assert.Equal(0, Sync());
        Assert.Equal("updated", File.ReadAllText(Path.Combine(Destination, "references", "format.txt")));
        Assert.Contains("2.0.0", File.ReadAllText(Manifest));

        Directory.Delete(Plugin, true);
        WritePackages(null, null);
        Assert.Equal(0, Sync([]));
        Assert.False(Directory.Exists(Destination));
        Assert.DoesNotContain("sample-skill", File.ReadAllText(Manifest));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("added")]
    [InlineData("removed")]
    public void PreservesLocallyModifiedSkillsDuringUpdateAndUninstall(string change)
    {
        Assert.Equal(0, Sync());
        var file = Path.Combine(Destination, "references", "format.txt");
        if (change == "changed") File.WriteAllText(file, "local");
        else if (change == "added") File.WriteAllText(Path.Combine(Destination, "local.txt"), "local");
        else File.Delete(file);

        File.WriteAllText(Path.Combine(Source, "references", "format.txt"), "upstream");
        Assert.Equal(1, Sync());
        Directory.Delete(Plugin, true);
        WritePackages(null, null);
        Assert.Equal(1, Sync([]));
        Assert.True(Directory.Exists(Destination));
        Assert.Contains("local changes", _errors.ToString());
    }

    [Fact]
    public void DoesNotOverwriteUnmanagedSkills()
    {
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Destination, "SKILL.md"), "user content");
        Assert.Equal(1, Sync());
        Assert.Equal("user content", File.ReadAllText(Path.Combine(Destination, "SKILL.md")));
        Assert.False(File.Exists(Manifest));
    }

    [Fact]
    public void MissingOrInvalidPackageDatabaseNeverCausesCleanup()
    {
        Assert.Equal(0, Sync());
        Directory.Delete(Plugin, true);
        var database = Path.Combine(Packages, "onewarestudio-packages.json");
        File.Delete(database);
        Assert.Equal(1, Sync([]));
        Assert.True(Directory.Exists(Destination));
        File.WriteAllText(database, "{invalid");
        Assert.Throws<JsonException>(() => Sync([]));
        Assert.True(Directory.Exists(Destination));
    }

    [Fact]
    public void DuplicateNamesFailBeforeWritingAnything()
    {
        var other = Path.Combine(_root, "another", "sample-skill");
        Directory.CreateDirectory(other);
        File.Copy(Path.Combine(Source, "SKILL.md"), Path.Combine(other, "SKILL.md"));
        var providers = new[]
        {
            new ExternalSkillProviderSource(Plugin, new Provider(SourceRoot)),
            new ExternalSkillProviderSource(Path.Combine(_root, "another"), new Provider(Path.GetDirectoryName(other)!))
        };
        Assert.Throws<InvalidDataException>(() => Sync(providers));
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public void CorruptManifestDoesNotReplaceExistingSkills()
    {
        Assert.Equal(0, Sync());
        File.WriteAllText(Manifest, "{}");
        File.WriteAllText(Path.Combine(Source, "references", "format.txt"), "upstream");
        Assert.Throws<JsonException>(() => Sync());
        Assert.Equal("reference", File.ReadAllText(Path.Combine(Destination, "references", "format.txt")));
    }

    [Fact]
    public void RemovesUnchangedSkillWhenManualProviderIsOmitted()
    {
        var providers = new[] { new ExternalSkillProviderSource(_root, new Provider(SourceRoot)) };
        Assert.Equal(0, Sync(providers));
        using (var json = JsonDocument.Parse(File.ReadAllText(Manifest)))
            Assert.StartsWith("path:", json.RootElement.GetProperty("Skills")
                .GetProperty("sample-skill").GetProperty("Source").GetString());
        Assert.Equal(0, Sync(providers));
        Assert.True(Directory.Exists(Destination));

        File.Delete(Path.Combine(Packages, "onewarestudio-packages.json"));
        Assert.Equal(0, Sync([]));
        Assert.False(Directory.Exists(Destination));
        Assert.DoesNotContain("sample-skill", File.ReadAllText(Manifest));
        Assert.Contains("Removed: sample-skill", _output.ToString());
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("added")]
    [InlineData("removed")]
    public void PreservesLocallyModifiedSkillWhenManualProviderIsOmitted(string change)
    {
        var providers = new[] { new ExternalSkillProviderSource(_root, new Provider(SourceRoot)) };
        Assert.Equal(0, Sync(providers));
        var file = Path.Combine(Destination, "references", "format.txt");
        if (change == "changed") File.WriteAllText(file, "local");
        else if (change == "added") File.WriteAllText(Path.Combine(Destination, "local.txt"), "local");
        else File.Delete(file);

        Assert.Equal(1, Sync([]));
        Assert.True(Directory.Exists(Destination));
        Assert.Contains("sample-skill", File.ReadAllText(Manifest));
        Assert.Contains("local changes", _errors.ToString());
    }

    [Fact]
    public void DoesNotRemoveBuiltInSkillWhenProviderIsOmitted()
    {
        var providers = new[] { new ExternalSkillProviderSource(Plugin, new Provider(SourceRoot), "studio", "1.0") };
        Assert.Equal(0, Sync(providers));
        Assert.Equal(0, Sync([]));
        Assert.True(Directory.Exists(Destination));
    }

    [Fact]
    public void BuiltInSkillUpdatesWhenStudioMovesToAnotherDirectory()
    {
        var provider = new[] { new ExternalSkillProviderSource(Plugin, new Provider(SourceRoot), "studio", "1.0") };
        Assert.Equal(0, Sync(provider));

        var movedRoot = Path.Combine(_root, "new-install", "ExternalSkills");
        var movedSkill = Path.Combine(movedRoot, "sample-skill");
        Directory.CreateDirectory(movedSkill);
        File.Copy(Path.Combine(Source, "SKILL.md"), Path.Combine(movedSkill, "SKILL.md"));
        File.WriteAllText(Path.Combine(movedSkill, "new.txt"), "new version");
        var movedProvider = new[]
        {
            new ExternalSkillProviderSource(Path.Combine(_root, "new-install"), new Provider(movedRoot), "studio", "2.0")
        };
        Assert.Equal(0, Sync(movedProvider));
        Assert.Equal("new version", File.ReadAllText(Path.Combine(Destination, "new.txt")));
        Assert.False(File.Exists(Path.Combine(Destination, "references", "format.txt")));
        Assert.Contains("2.0", File.ReadAllText(Manifest));
    }

    [Fact]
    public void RejectsMismatchedSkillName()
    {
        File.WriteAllText(Path.Combine(Source, "SKILL.md"), "---\nname: different\ndescription: Example\n---\n");
        Assert.Throws<InvalidDataException>(() => Sync());
        Assert.False(Directory.Exists(Destination));
    }

    private int Sync(IReadOnlyList<ExternalSkillProviderSource>? providers = null)
    {
        var synchronizer = new ExternalSkillSynchronizer(
            Documents, Plugins, Packages, "OneWare Studio", Path.Combine(_root, "home"), _output, _errors);
        return synchronizer.Synchronize(providers ?? [new ExternalSkillProviderSource(Plugin, new Provider(SourceRoot))]);
    }

    private void WritePackages(string? id, string? version)
    {
        Directory.CreateDirectory(Packages);
        var json = id is null
            ? "[]"
            : JsonSerializer.Serialize(new[] { new { Id = id, Type = "Plugin", Name = id, InstalledVersion = version } });
        File.WriteAllText(Path.Combine(Packages, "onewarestudio-packages.json"), json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        _output.Dispose();
        _errors.Dispose();
    }

    private sealed class Provider(string root) : IExternalAgentSkillProvider
    {
        public IReadOnlyCollection<string> GetSkillDirectories() => [root];
    }
}
