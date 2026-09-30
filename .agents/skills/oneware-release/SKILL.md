---
name: oneware-release
description: Prepares a OneWare Studio version bump (StudioVersion in Base.props, Flathub/AppStream metainfo release entry, changelog). Use when asked to bump the version, prepare a release, or write release notes/changelog entries for OneWare Studio.
---

# OneWare Studio release prep

A version bump touches three files. The `Publish Studio Desktop` workflow
(`.github/workflows/publish-studio-all.yml`) fails when the first `<release>` in the metainfo doesn't equal
`StudioVersion`, so keep them in sync.

1. **`build/props/Base.props`**: set `<StudioVersion>X.Y.Z</StudioVersion>`. Core, Essentials and
   UniversalFpgaProjectSystem package versions derive from it.
2. **`studio/OneWare.Studio.Desktop/com.one_ware.OneWare.metainfo.xml`**: add
   `<release version="X.Y.Z" date="YYYY-MM-DD" type="stable"/>` as the **first** child of `<releases>`, using
   today's date.
3. **`docs/changelog.md`**: add a `## X.Y.Z` section at the top with short, user-facing bullets
   (`Added ...`, `Improved ...`, `Fixed ...`). Build them from `git log <previous-tag>..HEAD` and merged PRs.
   Leave out refactorings, CI and dependency-bot noise unless the user can notice them.

Rules:

- Ask for the version number if the user didn't give one. Don't guess between patch and minor bumps.
- Don't create tags or GitHub releases. The publish workflow does that from `StudioVersion`.
- The commit is usually named just `X.Y.Z`.

Validation: `git diff` shows exactly those three files, and the version string is identical in
`Base.props` and the first metainfo `<release>`.
