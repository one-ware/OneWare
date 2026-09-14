# Package Manager

The Package Manager is an in-app browser for the configured package catalogs and locally
installed packages. It is not a separate marketplace service.

## Browse and discover

- **Discover** shows the catalog, including packages already installed.
- **Installed** uses installed-version metadata, so installed packages remain visible even
  when their remote manifest is unavailable.
- **Updates (count)** lists available updates. **Update All** reviews a joint plan for all
  available updates, not just the rows matching the current search/category. It is disabled
  while refreshing, while a package is installing, and when no updates are available.
- The 44-pixel search field matches whitespace-separated tokens, ignoring case, across
  package name, ID, description, category and type. All tokens must match. Exact name/ID
  matches rank first, then name/ID token matches, then descriptive matches. Search is
  debounced by 200 ms; this is substring matching, not fuzzy search.
- The category dropdown includes built-in and dynamically registered category paths.
  Search, category and page filters combine. **Clear** clears only search; **Reset filters**
  clears search/category without leaving the current page.
- The list is virtualized. Rows show icon (or placeholder), description, category, status,
  installed version and contextual Install, Update or Cancel actions. Installed rows show
  a neutral **Installed** state and **Details**, never Remove as their primary action.

### Featured by OneWare

The unfiltered Discover page shows up to three curated cards supplied by the discovery
service. The official-source seed is the real `OneWare.AI` package (**ONE AI Extension**),
not the Copilot CLI package. The seed only references a package present in a configured
official catalog. Repository `featuredPackageIds` can extend this collection.

Promotion is tied to the winning catalog source. A custom override with the same ID does
not inherit official promotion; custom-only sources do not cause official packages to be
injected. Missing featured packages are omitted. Cards open details and never install
on selection; an installed featured package still opens details rather than offering Remove.
Promotion disappears immediately when a search/category filter is active or another page
is selected.

## Details and navigation

**Details**, double-clicking a row, or Enter on the package list opens a full details page.
Arrow-key list selection alone does not navigate or install. **Back to packages** (or
Alt+Left) restores the browse page, query, category and saved list scroll offset. The browse
controls stay alive, and unchanged rows are not rebuilt on status updates. Ctrl+F returns
to browse and focuses search.

Details include identity/icon, installed and latest known versions, links, About and
manifest-defined tabs such as Changelog and License. **Version and platform options**
contains the version selector and target list. Compatibility warnings remain visible.
Optional tab/icon failures do not prevent installation review. A failed tab shows an
unavailable message; a failed icon uses a placeholder. Compatibility previews are advisory:
installation still requires service-side validation.

**Dependencies** lists the selected version's declared direct requirements, inclusive
minimum/exclusive maximum, installed version and whether that version satisfies the range.
Available dependency IDs are clickable and open their own details even when excluded by
the browse filters. Missing IDs are explicitly unavailable, not download links. The install
review includes transitive dependencies. For an installed-only package, details use its
installed dependency metadata, distinguishing unavailable legacy metadata from an empty list.

**More package actions → Remove** asks for confirmation. The service can block removal when
another installed package requires it, including offline. Removing a parent retains its
dependencies; there is no cascade uninstall or automatic unused-dependency cleanup.

## Install, update and consent

Row/details installation, quick install and Update All use the shared `PackageOperationReview`:

1. Request a plan from `IPackageOperationService`.
2. Show each package, action, current/selected versions and “required by” reason.
3. Download every required license independently of details tabs. The current license
   endpoint reads the manifest tab whose title is exactly `License`.
4. Obtain one explicit confirmation for the plan and all required licenses. A missing,
   failed or declined required license stops before execution.
5. Execute the reviewed plan with only those accepted package IDs. The backend revalidates
   the plan; changed plans require another review rather than silently selecting new versions.

There is no UI fallback to direct installation if planning is unavailable. Structured failure
messages include service diagnostics, completed/retained packages and restart requirements.
Quick install has a stable Review changes command, cancellation and persistent result text;
it does not show a second generic failure dialog. Cancelling before execution is not reported
as an installation error. Cancellation after partial completion reports retained changes.

The backend stages and validates downloads before activation, installs dependencies before
dependents, and coordinates mutations. Already completed packages can remain after a later
failure or cancellation. There is no full graph-wide rollback or hot-unload guarantee.
Plugin changes that need a restart are reflected by package status and a consolidated banner.

## Sources and offline use

**Sources** opens Package Manager settings. **Refresh** retries configured sources and shows
a warning when sources fail; installed content is retained. The default desktop catalog uses
the configured cloud endpoint with the official GitHub fallback. Environment/custom-source
configuration remains authoritative; this UI does not register extra sources.

## Current limitations and validation

- Featured cards use View details rather than a separate one-click install button. There are
  no ratings, reviews, fabricated publisher statistics or self-promoted manifest flags.
- Progress is the existing per-package service progress/indeterminate state. There is no
  aggregate package n/N or named staging/activation phase UI yet.
- Back returns to browse, not through a stack of dependency detail pages. Scroll offsets are
  clamped naturally if filtering or a catalog update removes rows while details are open.
- The latest-known label includes the latest catalog version, potentially prerelease, or
  just the stored installed version when no catalog metadata is available. The default
  selected install/update version follows the installed release channel and Studio support.
- Dependencies are required plugin-to-plugin only. Optional dependencies, arbitrary range
  expressions, native dependency alternatives and automatic downgrades are unsupported.
- Optional content is loaded lazily after opening details, but manifest tabs are fetched
  sequentially, not individually on tab selection. Legacy license downloads have no
  cancellation-token parameter; cancellation is checked again before confirmation/execution.
- Refresh warnings are based on the refresh initiated by this window; the service does not
  expose a persistent last-source-error snapshot to newly opened windows.
- Existing profile/background callers remain backend-guarded but do not gain this review UI
  automatically; consent-required results must be handled by the caller.
- Removal's public API returns a boolean, so the UI shows a general blocked/failure message;
  exact reverse-dependency diagnostics remain in the service log.
- The layout targets 800-pixel-and-wider windows. Theme/DPI, screen reader behaviour and
  real network/install/restart scenarios still require desktop manual validation.

Focused headless and view-model regressions are in
[PackageBrowseTests.cs](../tests/OneWare.PackageManager.UnitTests/PackageBrowseTests.cs), covering
search/filtering, navigation/scroll, observable timing, offline content, dependency links,
required consent and quick-install result handling. Validation on Windows with .NET 10:
131 package-manager tests and 26 desktop tests pass; the desktop application builds.
Live network installs, real dependent-plugin cold startup, and visual/theme/DPI checks remain
manual validation tasks. Dependency activation is not a crash-recovery transaction spanning
all packages, and metadata fingerprints do not authenticate archive bytes behind a URL.

For package authors, see [PluginDevelopment.md](PluginDevelopment.md#versioned-package-dependencies).