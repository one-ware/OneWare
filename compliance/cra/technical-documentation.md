# Technical Documentation (Annex VII) — OneWare Studio

> Skeleton for the CRA technical file, required **only if OneWare Studio is
> treated as a manufacturer** (see [classification.md](classification.md) §1).
> Retain the completed file for **10 years**.

## 1. General Product Description

- **Manufacturer:** ONE WARE GmbH, Annengasse 3, 33034 Brakel, Germany — `info@one-ware.com`.
- **Product:** OneWare Studio (desktop IDE for electronics development), Apache-2.0.
- **Versions / identifiers:** release version + build; channels (download, Snap,
  Flathub, WinGet, web) — _TODO_.
- **Intended use / foreseeable misuse:** _TODO_.

## 2. Design & Development Documentation

- **Architecture:** .NET 10 modular application on Avalonia UI; extensible plugin
  architecture; solution `OneWare.slnx` (`src/`, `studio/`).
- **Connectivity:** Git integration, extension marketplace, update checks, and
  optional cloud connectivity via the OneWare.AI extension. _TODO: data-flow diagram._
- **Cryptographic measures:** TLS for network calls; update/package signing
  (Windows + macOS signed; Linux TODO). _Document algorithms & key management._
- **Software components (SBOM):** [`.github/workflows/sbom.yml`](../../.github/workflows/sbom.yml) (CycloneDX).

## 3. Cybersecurity Risk Assessment

- Threat model (assets, actors, attack vectors — e.g. malicious extensions,
  update tampering, project-file parsing) — _TODO_.
- Methodology, risk treatment, residual risks — _TODO_.

## 4. Implementation of Essential Requirements

- Map each Annex I requirement to a measure. **Source:** [gap-analysis.md](gap-analysis.md).

## 5. Test Reports & Evidence

- Security tests, SAST/DAST, code review — _TODO_.
- CI: [`.github/workflows/test.yml`](../../.github/workflows/test.yml).

## 6. Vulnerability Handling

- Public policy: [../../SECURITY.md](../../SECURITY.md).
- Internal process: [vulnerability-handling-policy.md](vulnerability-handling-policy.md).

## 7. EU Declaration of Conformity

- [declaration-of-conformity-template.md](declaration-of-conformity-template.md).

## 8. Standards Applied

- _TODO_ — candidates: ISO/IEC 29147 (disclosure), ISO/IEC 30111 (handling),
  NIST SSDF (SP 800-218), ISO/IEC 27001.
