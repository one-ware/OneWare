# Git support

## Working with changes

- **Changes** compares the working tree with the index (staging area), including new files.
- **Staged Changes** compares the index with HEAD, including the first commit in a new repository.
- A file with both staged and unstaged edits appears in both lists; each opens its own comparison tab.
- **Commit Staged** commits only the index. **Commit All** stages and commits non-ignored changes.
- Unresolved conflicts must be resolved and explicitly staged before committing. Commit All does not silently resolve them.
- **Undo Changes** asks for confirmation and restores from the index, keeping staged edits.
- **Undo All Changes** confirms a reset of staged and unstaged tracked changes to HEAD. Untracked files are kept; if an untracked file would be overwritten, the reset is refused.
- Deleting an individual untracked file requires confirmation and is permanent.
- **Open File (Head)** opens a separate read-only temporary snapshot, not the working editor.

## Branches and remotes

- Checking out remote branches preserves full names such as `feature/team/task` and sets tracking. An unrelated local branch with the same name is not silently reused.
- Push can publish a branch to a selected remote. Upstream configuration is saved only after a successful push.
- Remote deletion requires explicit confirmation; failed deletion keeps the local tracking reference.
- Sync holds one operation lock across pull and push. A failed pull or merge conflict prevents the subsequent push.
- Fetch and local polling use independent timers. Worktree scanning runs off the UI thread.
- Background fetch does not open login dialogs or permanently disable itself after a network failure. Manual fetch can request authentication.
- Clone checks its destination and offers to open the result as a folder project. Cancellation leaves downloaded files in place.

Authentication still uses the existing credential store and login providers. This change does not add stash, rebase, history browsing, or new authentication providers.

## Validation

Automated regression tests are in [GitOperationsTests.cs](../tests/OneWare.SourceControl.UnitTests/GitOperationsTests.cs). They use temporary repositories and local bare remotes, without production credentials or remote writes.

Manual smoke checks for a running desktop build:

1. Stage a file, edit it again, and compare each list. Commit Staged from both the menu and button must leave the second edit uncommitted.
2. Cancel discard and identity dialogs; verify files, index and commit message remain unchanged and commands become usable again.
3. Enable both timers; verify local changes refresh while remote ahead/behind counts also update.
4. Change the active project during a slow fetch; verify the operation finishes on its original repository and the new project's status is shown afterward.
5. Clone into a chosen parent directory and open the result. Check cancellation and a non-empty destination as well.
6. Publish, pull and push with a private repository using interactive authentication. Check rejected pushes and login cancellation.