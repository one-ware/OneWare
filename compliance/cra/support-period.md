# Support Period & End-of-Life Policy — OneWare Studio

> Workflow 5. **Status: 5-year commitment; user-facing communication TODO.**

## Commitment

The CRA requires a support period of **at least 5 years** (or the expected product
lifetime if shorter), clearly communicated to users.

ONE WARE GmbH commits to a **5-year** security-update support period for OneWare
Studio from each release.

| Aspect | Commitment | Communicated where |
|---|---|---|
| Security-update support | **5 years** from release | _TODO: docs / download page (https://one-ware.com/docs/studio/setup)_ |
| Delivery channels | Direct download, Snap, Flathub, WinGet, web | Release notes |

## During the Support Period

- Free-of-charge security updates within the SLAs in
  [vulnerability-handling-policy.md](vulnerability-handling-policy.md).
- Updates must not degrade functionality or security.
- Keep the SBOM current (automated).

## End of Life

- Notify users **at least 1 year** before end of security support.
- Provide a final cumulative security update where feasible.
- Document the decision in the technical file.

## Multi-Component Dependency Note

OneWare Studio depends on the .NET runtime, Avalonia and other components with
their own lifecycles. _TODO: map key upstream support windows (.NET LTS, Avalonia)
against the committed 5-year period and patch/upgrade accordingly._
