using LibGit2Sharp;

namespace OneWare.SourceControl;

/// <summary>Git operations shared by the UI and repository-level regression tests.</summary>
public static class GitOperations
{
    public static string GetCloneDirectoryName(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var path = url.Trim().TrimEnd('/', '\\');
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            path = uri.IsFile ? uri.LocalPath : uri.AbsolutePath.TrimEnd('/');
        var name = path[(path.LastIndexOfAny(new[] { '/', '\\', ':' }) + 1)..];
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The repository URL does not contain a valid folder name.", nameof(url));
        return name;
    }

    public static string GetRelativePath(Repository repository, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = repository.Info.WorkingDirectory;
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path, root)).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ArgumentException("The file is outside this repository.", nameof(path));
        return relative;
    }

    public static string GetRemoteBranchName(Branch branch)
    {
        if (!branch.IsRemote) throw new ArgumentException("Expected a remote branch.", nameof(branch));
        var prefix = $"refs/remotes/{branch.RemoteName}/";
        if (!branch.CanonicalName.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("Cannot determine the remote branch name.", nameof(branch));
        return branch.CanonicalName[prefix.Length..];
    }

    public static Branch CheckoutBranch(Repository repository, Branch branch)
    {
        if (branch.IsRemote)
        {
            var name = GetRemoteBranchName(branch);
            var local = repository.Branches[name];
            if (local == null)
            {
                local = repository.CreateBranch(name, branch.Tip);
                local = repository.Branches.Update(local, b => b.TrackedBranch = branch.CanonicalName);
            }
            else if (local.TrackedBranch?.CanonicalName != branch.CanonicalName)
            {
                throw new InvalidOperationException($"Local branch '{name}' already exists and does not track '{branch.FriendlyName}'.");
            }
            branch = local;
        }
        return Commands.Checkout(repository, branch);
    }

    public static Commit Commit(Repository repository, string message, Signature signature, bool stagedOnly)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Enter a commit message before committing.", nameof(message));
        // Do not let 'Commit All' silently mark unresolved conflicts as resolved.
        if (repository.Index.Conflicts.Any())
            throw new InvalidOperationException("Resolve and stage merge conflicts before committing.");
        if (!stagedOnly) Commands.Stage(repository, "*");
        return repository.Commit(message, signature, signature);
    }

    public static Branch PublishBranch(Repository repository, string remoteName, PushOptions options)
    {
        if (repository.Info.IsHeadDetached || repository.Head.Tip == null)
            throw new InvalidOperationException("Check out a branch with at least one commit before publishing.");
        var head = repository.Head;
        repository.Network.Push(repository.Network.Remotes[remoteName],
            $"{head.CanonicalName}:{head.CanonicalName}", options);
        // Failed pushes must not leave behind upstream configuration.
        return repository.Branches.Update(head, b => b.Remote = remoteName, b => b.UpstreamBranch = head.CanonicalName);
    }

    public static void DeleteRemoteBranch(Repository repository, Branch branch, PushOptions options)
    {
        var name = GetRemoteBranchName(branch);
        repository.Network.Push(repository.Network.Remotes[branch.RemoteName], $":refs/heads/{name}", options);
        // libgit2 may already have removed the local tracking ref after the push.
        if (repository.Branches[branch.CanonicalName] is { } remaining) repository.Branches.Remove(remaining);
    }

    public static void DiscardWorkingTreeFile(Repository repository, string path)
    {
        path = GetRelativePath(repository, path);
        if (repository.Index[path] == null)
            throw new InvalidOperationException("This file is not in the index. Delete untracked files separately.");
        // Restore from the index, NOT HEAD: preserve any already staged changes.
        var tree = repository.Index.WriteToTree();
        repository.Checkout(tree, new[] { path }, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
    }

    public static void ResetTrackedChanges(Repository repository, ResetMode mode)
    {
        if (mode == ResetMode.Hard)
        {
            var head = repository.Head.Tip ?? throw new InvalidOperationException("There is no commit to reset to.");
            var status = repository.RetrieveStatus(new StatusOptions { RecurseUntrackedDirs = true });
            foreach (var entry in status.Where(x => x.State.HasFlag(FileStatus.NewInWorkdir)))
            {
                // A hard reset can overwrite an untracked file if it obstructs a HEAD entry.
                // Keep the UI's promise to preserve untracked files, even after a staged deletion.
                var path = entry.FilePath.TrimEnd('/');
                if (head[path] != null)
                    throw new InvalidOperationException($"Move untracked file '{path}' before resetting; it would be overwritten.");
                while (path.Contains('/'))
                {
                    path = path[..path.LastIndexOf('/')];
                    if (head[path]?.TargetType == TreeEntryTargetType.Blob)
                        throw new InvalidOperationException($"Move untracked file '{entry.FilePath}' before resetting; it would be overwritten.");
                }
            }
        }
        repository.Reset(mode);
    }

    public static Patch GetPatch(Repository repository, string path, int contextLines, bool staged)
    {
        var paths = new[] { GetRelativePath(repository, path) };
        var options = new CompareOptions { ContextLines = contextLines };
        return staged
            ? repository.Diff.Compare<Patch>(repository.Head.Tip?.Tree, DiffTargets.Index, paths,
                new ExplicitPathsOptions(), options)
            : repository.Diff.Compare<Patch>(paths, true, new ExplicitPathsOptions(), options);
    }
}