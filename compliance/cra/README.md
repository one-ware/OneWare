# EU Cyber Resilience Act (CRA) Compliance — OneWare Studio

This folder holds ONE WARE GmbH's compliance documentation for **Regulation (EU)
2024/2847 — the Cyber Resilience Act (CRA)** as it applies to **OneWare Studio**,
a free and open-source (GPL-3.0) IDE for electronics development.

> **Status:** Working documents. The single most important open question is the
> **open-source classification** (§1 of [classification.md](classification.md)):
> whether ONE WARE GmbH acts as a *manufacturer* or an *open-source software
> steward* for OneWare Studio. This is flagged for legal review.
>
> This is general compliance structure, not legal advice.

## Key Dates

| Milestone | Date |
|---|---|
| CRA entered into force | 10 December 2024 |
| Vulnerability & incident reporting obligations apply | **11 September 2026** |
| **Full application (all obligations)** | **11 December 2027** |

## Documents

| Document | Covers |
|---|---|
| [../../SECURITY.md](../../SECURITY.md) | Coordinated Vulnerability Disclosure policy & public point of contact |
| [classification.md](classification.md) | Scope, product class, manufacturer-vs-steward analysis |
| [gap-analysis.md](gap-analysis.md) | Annex I Part I & II gap analysis |
| [vulnerability-handling-policy.md](vulnerability-handling-policy.md) | Internal handling, SBOM, ENISA/CSIRT reporting |
| [support-period.md](support-period.md) | Support-period commitment & end-of-life |
| [technical-documentation.md](technical-documentation.md) | Technical documentation skeleton (Annex VII) |
| [declaration-of-conformity-template.md](declaration-of-conformity-template.md) | EU Declaration of Conformity template (Annex V) |

## Automation in this repository

| Artifact | Purpose |
|---|---|
| [`.github/workflows/sbom.yml`](../../.github/workflows/sbom.yml) | CycloneDX SBOM for the desktop application |
| [`.github/workflows/dependency-scan.yml`](../../.github/workflows/dependency-scan.yml) | Fails CI on known-vulnerable NuGet packages |
| [`.github/dependabot.yml`](../../.github/dependabot.yml) | Automated dependency-update PRs |

## Ownership

| Responsibility | Owner |
|---|---|
| CRA programme owner | Hendrik Mennen (CTO) |
| Security / vulnerability handling lead | Hendrik Mennen (CTO) — _backup TODO_ |
| ENISA/CSIRT accountable reporter | Hendrik Mennen (CTO); CSIRT: BSI/CERT-Bund (DE) |
