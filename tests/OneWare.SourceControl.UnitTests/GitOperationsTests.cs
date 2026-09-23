using LibGit2Sharp;
using OneWare.SourceControl.Converters;
using OneWare.SourceControl.Models;
using Xunit;

namespace OneWare.SourceControl.UnitTests;

public sealed class GitOperationsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OneWare.Git.Tests", Guid.NewGuid().ToString("N"));
    private readonly Repository _repository;
    private readonly Signature _signature = new("Git Tests", "git-tests@example.invalid", DateTimeOffset.Now);

    public GitOperationsTests()
    {
        Repository.Init(_directory);
        _repository = new Repository(_directory);
        _repository.Refs.UpdateTarget("HEAD", "refs/heads/test-main");
        _repository.Config.Set("core.autocrlf", false);
        _repository.Config.Set("commit.gpgsign", false);
    }

    [Fact]
    public void StagedCommitPreservesUnstagedEditsAndUntrackedFiles()
    {
        CommitFile("file.txt", "base\n");
        Write("file.txt", "staged\n");
        Commands.Stage(_repository, "file.txt");
        Write("file.txt", "unstaged\n");
        Write("other.txt", "untracked\n");

        var commit = GitOperations.Commit(_repository, "staged only", _signature, stagedOnly: true);

        Assert.Equal("staged\n", ((Blob)commit["file.txt"].Target).GetContentText());
        Assert.Null(commit["other.txt"]);
        Assert.Equal("unstaged\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
        Assert.True(_repository.RetrieveStatus("file.txt").HasFlag(FileStatus.ModifiedInWorkdir));
        Assert.Equal(FileStatus.NewInWorkdir, _repository.RetrieveStatus("other.txt"));
    }

    [Fact]
    public void CommitAllIncludesChangesButHonorsGitignore()
    {
        Write(".gitignore", "ignored.txt\n");
        Write("tracked.txt", "included\n");
        Write("ignored.txt", "not included\n");

        var commit = GitOperations.Commit(_repository, "initial", _signature, stagedOnly: false);

        Assert.NotNull(commit["tracked.txt"]);
        Assert.Null(commit["ignored.txt"]);
        Assert.Equal(FileStatus.Ignored, _repository.RetrieveStatus("ignored.txt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void InvalidCommitMessageDoesNotStageFiles(string message)
    {
        Write("file.txt", "untracked\n");

        Assert.Throws<ArgumentException>(() => GitOperations.Commit(_repository, message, _signature, stagedOnly: false));

        Assert.Empty(_repository.Index);
        Assert.Null(_repository.Head.Tip);
    }

    [Fact]
    public void CommitAllDoesNotSilentlyResolveConflicts()
    {
        var initial = CommitFile("file.txt", "base\n");
        var mainBranch = _repository.Head.FriendlyName;
        var incoming = _repository.CreateBranch("incoming", initial);
        Commands.Checkout(_repository, incoming);
        CommitFile("file.txt", "incoming\n");
        Commands.Checkout(_repository, mainBranch);
        CommitFile("file.txt", "current\n");
        var result = _repository.Merge(_repository.Branches["incoming"], _signature);
        Assert.Equal(MergeStatus.Conflicts, result.Status);
        var tip = _repository.Head.Tip.Id;

        Assert.Throws<InvalidOperationException>(() => GitOperations.Commit(_repository, "unresolved", _signature, stagedOnly: false));

        Assert.NotEmpty(_repository.Index.Conflicts);
        Assert.Equal(tip, _repository.Head.Tip.Id);
    }

    [Fact]
    public void DiscardRestoresIndexWithoutLosingStagedChanges()
    {
        CommitFile("file.txt", "base\n");
        Write("file.txt", "staged\n");
        Commands.Stage(_repository, "file.txt");
        var stagedBlob = _repository.Index["file.txt"].Id;
        Write("file.txt", "unstaged\n");
        Write("unrelated.txt", "keep me\n");
        var tip = _repository.Head.Tip.Id;

        GitOperations.DiscardWorkingTreeFile(_repository, "file.txt");

        Assert.Equal("staged\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
        Assert.Equal(stagedBlob, _repository.Index["file.txt"].Id);
        Assert.Equal(FileStatus.ModifiedInIndex, _repository.RetrieveStatus("file.txt"));
        Assert.Equal(tip, _repository.Head.Tip.Id);
        Assert.True(File.Exists(Path.Combine(_directory, "unrelated.txt")));
    }

    [Fact]
    public void DiscardRestoresDeletedWorkingFile()
    {
        CommitFile("file.txt", "base\n");
        File.Delete(Path.Combine(_directory, "file.txt"));

        GitOperations.DiscardWorkingTreeFile(_repository, "file.txt");

        Assert.Equal("base\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
        Assert.Equal(FileStatus.Unaltered, _repository.RetrieveStatus("file.txt"));
    }

    [Fact]
    public void DiscardWorksBeforeFirstCommit()
    {
        Write("file.txt", "staged\n");
        Commands.Stage(_repository, "file.txt");
        Write("file.txt", "unstaged\n");

        GitOperations.DiscardWorkingTreeFile(_repository, "file.txt");

        Assert.Equal("staged\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
        Assert.Equal(FileStatus.NewInIndex, _repository.RetrieveStatus("file.txt"));
        Assert.Null(_repository.Head.Tip);
    }

    [Fact]
    public void DiscardRefusesUntrackedFileWithoutDeletingIt()
    {
        Write("file.txt", "keep\n");

        Assert.Throws<InvalidOperationException>(() => GitOperations.DiscardWorkingTreeFile(_repository, "file.txt"));

        Assert.Equal("keep\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
    }

    [Fact]
    public void StagedAndUnstagedDiffsUseDifferentBaselines()
    {
        CommitFile("folder/file.txt", "base\n");
        Write("folder/file.txt", "staged\n");
        Commands.Stage(_repository, "folder/file.txt");
        Write("folder/file.txt", "unstaged\n");

        using var staged = GitOperations.GetPatch(_repository, Path.Combine(_directory, "folder", "file.txt"), 3, staged: true);
        using var unstaged = GitOperations.GetPatch(_repository, "folder/file.txt", 3, staged: false);

        Assert.Contains("-base\n", staged.Content);
        Assert.Contains("+staged\n", staged.Content);
        Assert.DoesNotContain("+unstaged\n", staged.Content);
        Assert.Contains("-staged\n", unstaged.Content);
        Assert.Contains("+unstaged\n", unstaged.Content);
    }

    [Fact]
    public void StagedDiffSupportsInitialCommit()
    {
        Write("file.txt", "initial\n");
        Commands.Stage(_repository, "file.txt");

        using var patch = GitOperations.GetPatch(_repository, "file.txt", 3, staged: true);

        Assert.Contains("+initial\n", patch.Content);
    }

    [Fact]
    public void WorkingTreeDiffIncludesUntrackedFiles()
    {
        Write("file.txt", "untracked\n");

        using var patch = GitOperations.GetPatch(_repository, "file.txt", 3, staged: false);

        Assert.Contains("+untracked\n", patch.Content);
    }

    [Theory]
    [InlineData("feature/nested/name")]
    [InlineData("main")]
    public void RemoteCheckoutPreservesFullBranchNameAndTracking(string name)
    {
        var commit = CommitFile("file.txt", "base\n");
        _repository.Network.Remotes.Add("upstream", Path.Combine(_directory, "remote.git"));
        _repository.Refs.Add($"refs/remotes/upstream/{name}", commit.Id);
        var remote = _repository.Branches[$"upstream/{name}"];

        Assert.Equal(name, GitOperations.GetRemoteBranchName(remote));
        var branch = GitOperations.CheckoutBranch(_repository, remote);

        Assert.Equal(name, branch.FriendlyName);
        Assert.Equal(remote.CanonicalName, branch.TrackedBranch.CanonicalName);
        Assert.Equal(branch.CanonicalName, _repository.Head.CanonicalName);
    }

    [Fact]
    public void RemoteCheckoutDoesNotHijackUnrelatedLocalBranch()
    {
        var commit = CommitFile("file.txt", "base\n");
        _repository.CreateBranch("feature/nested", commit);
        _repository.Network.Remotes.Add("origin", Path.Combine(_directory, "remote.git"));
        _repository.Refs.Add("refs/remotes/origin/feature/nested", commit.Id);
        var head = _repository.Head.CanonicalName;

        Assert.Throws<InvalidOperationException>(() => GitOperations.CheckoutBranch(_repository, _repository.Branches["origin/feature/nested"]));

        Assert.Equal(head, _repository.Head.CanonicalName);
        Assert.False(_repository.Branches["feature/nested"].IsTracking);
    }

    [Fact]
    public void RejectsPathsOutsideRepository()
    {
        Assert.Throws<ArgumentException>(() => GitOperations.GetRelativePath(_repository, "../outside.txt"));
        Assert.Throws<ArgumentException>(() => GitOperations.GetRelativePath(_repository, Path.Combine(Path.GetTempPath(), "outside.txt")));
    }

    [Fact]
    public void StatusReplacementNotifiesBindings()
    {
        Write("file.txt", "new\n");
        var status = _repository.RetrieveStatus().Single();
        var model = new SourceControlFileModel(Path.Combine(_directory, "file.txt"), status);
        var properties = new List<string?>();
        model.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        Commands.Stage(_repository, "file.txt");

        model.Status = _repository.RetrieveStatus().Single();

        Assert.Contains(nameof(SourceControlFileModel.Status), properties);
        Assert.Equal(FileStatus.NewInIndex, model.Status.State);
    }

    [Fact]
    public void PublishNestedBranchSetsUpstreamAfterSuccessfulPush()
    {
        CommitFile("file.txt", "base\n");
        Commands.Checkout(_repository, _repository.CreateBranch("feature/nested/name"));
        var remotePath = Path.Combine(_directory, "remote.git");
        Repository.Init(remotePath, isBare: true);
        _repository.Network.Remotes.Add("upstream", remotePath);

        var published = GitOperations.PublishBranch(_repository, "upstream", new PushOptions());

        using var remote = new Repository(remotePath);
        Assert.Equal(_repository.Head.Tip.Id, remote.Branches["feature/nested/name"].Tip.Id);
        Assert.Equal("upstream", published.RemoteName);
        Assert.Equal("refs/remotes/upstream/feature/nested/name", published.TrackedBranch.CanonicalName);
    }

    [Fact]
    public void FailedPublishDoesNotSetUpstream()
    {
        CommitFile("file.txt", "base\n");
        _repository.Network.Remotes.Add("origin", new Uri(Path.Combine(_directory, "missing.git")).AbsoluteUri);

        Assert.ThrowsAny<LibGit2SharpException>(() => GitOperations.PublishBranch(_repository, "origin", new PushOptions()));

        Assert.False(_repository.Head.IsTracking);
        Assert.Null(_repository.Config.Get<string>("branch.test-main.remote"));
        Assert.Null(_repository.Config.Get<string>("branch.test-main.merge"));
    }

    [Fact]
    public void RemoteDeletionPreservesFullNestedName()
    {
        CommitFile("file.txt", "base\n");
        Commands.Checkout(_repository, _repository.CreateBranch("feature/nested/name"));
        var remotePath = Path.Combine(_directory, "remote.git");
        Repository.Init(remotePath, isBare: true);
        _repository.Network.Remotes.Add("upstream", remotePath);
        GitOperations.PublishBranch(_repository, "upstream", new PushOptions());
        var branch = _repository.Branches["upstream/feature/nested/name"];

        GitOperations.DeleteRemoteBranch(_repository, branch, new PushOptions());

        using var remote = new Repository(remotePath);
        Assert.Null(remote.Branches["feature/nested/name"]);
        Assert.Null(_repository.Branches["upstream/feature/nested/name"]);
        Assert.NotNull(_repository.Branches["feature/nested/name"]);
    }

    [Fact]
    public void FailedRemoteDeletionKeepsLocalTrackingRef()
    {
        var commit = CommitFile("file.txt", "base\n");
        _repository.Network.Remotes.Add("origin", new Uri(Path.Combine(_directory, "missing.git")).AbsoluteUri);
        _repository.Refs.Add("refs/remotes/origin/feature/nested", commit.Id);
        var branch = _repository.Branches["origin/feature/nested"];

        Assert.ThrowsAny<LibGit2SharpException>(() => GitOperations.DeleteRemoteBranch(_repository, branch, new PushOptions()));

        Assert.Equal(commit.Id, _repository.Branches["origin/feature/nested"].Tip.Id);
    }

    [Theory]
    [InlineData("https://github.com/team/repository.git", "repository")]
    [InlineData("https://github.com/team/repository.git/", "repository")]
    [InlineData("https://github.com/team/repository.git?query=value", "repository")]
    [InlineData("git@github.com:team/repository.git", "repository")]
    [InlineData("ssh://git@example.com/team/repository.git", "repository")]
    [InlineData("https://example.com/team/repository.name", "repository.name")]
    [InlineData("https://example.com/team/repository.name.git", "repository.name")]
    public void CloneFolderNamesHandleCommonUrls(string url, string expected)
    {
        Assert.Equal(expected, GitOperations.GetCloneDirectoryName(url));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("https://example.com/")]
    [InlineData("..")]
    [InlineData(".")]
    public void CloneRejectsMissingFolderName(string url)
    {
        Assert.Throws<ArgumentException>(() => GitOperations.GetCloneDirectoryName(url));
    }

    [Theory]
    [InlineData(FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir, "M")]
    [InlineData(FileStatus.NewInIndex | FileStatus.DeletedFromWorkdir, "D")]
    [InlineData(FileStatus.RenamedInIndex | FileStatus.ModifiedInWorkdir, "R")]
    [InlineData(FileStatus.Conflicted | FileStatus.ModifiedInWorkdir, "U")]
    [InlineData(FileStatus.NewInWorkdir, "+")]
    public void CombinedStatusFlagsHaveUsefulLabels(FileStatus status, string label)
    {
        var converter = new ChangeStatusCharConverter();
        Assert.Equal(label, converter.Convert(status, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void HardResetKeepsUntrackedFiles()
    {
        CommitFile("file.txt", "base\n");
        Write("file.txt", "changed\n");
        Write("untracked.txt", "keep\n");

        GitOperations.ResetTrackedChanges(_repository, ResetMode.Hard);

        Assert.Equal("base\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
        Assert.Equal("keep\n", File.ReadAllText(Path.Combine(_directory, "untracked.txt")));
    }

    [Fact]
    public void HardResetRefusesToOverwriteUntrackedFileAfterStagedDeletion()
    {
        CommitFile("file.txt", "base\n");
        _repository.Index.Remove("file.txt");
        _repository.Index.Write();
        Write("file.txt", "untracked replacement\n");

        Assert.Throws<InvalidOperationException>(() => GitOperations.ResetTrackedChanges(_repository, ResetMode.Hard));

        Assert.Equal("untracked replacement\n", File.ReadAllText(Path.Combine(_directory, "file.txt")));
        Assert.Null(_repository.Index["file.txt"]);
    }

    private Commit CommitFile(string path, string content)
    {
        Write(path, content);
        Commands.Stage(_repository, path);
        return _repository.Commit("test commit", _signature, _signature);
    }

    private void Write(string path, string content)
    {
        var fullPath = Path.Combine(_directory, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose()
    {
        _repository.Dispose();
        foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_directory, recursive: true);
    }
}