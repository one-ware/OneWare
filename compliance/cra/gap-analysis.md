# Annex I Essential-Requirements Gap Analysis — OneWare Studio

> Workflow 2 — Annex I gap analysis. **Status: DRAFT.**
> Status legend: ✅ Met · 🟡 Partial · ❓ To verify · ❌ Gap · ➖ N/A
>
> Applies if OneWare Studio is treated as a manufacturer (see
> [classification.md](classification.md) §1). Many controls also demonstrate good
> practice under the open-source steward regime.

## Annex I — Part I: Security Properties

| # | Requirement | Status | Notes / Evidence | Action |
|---|---|---|---|---|
| 1 | No known exploitable vulnerabilities at release | 🟡 | CI build & test (`.github/workflows/test.yml`). Dependency scanning added (`.github/workflows/dependency-scan.yml`, `.github/dependabot.yml`). | Make the scan a required release gate. |
| 2 | Secure by default | 🟡 | Desktop IDE with local-first defaults. | Document default network/telemetry behaviour and how to disable optional connectivity. |
| 3 | Protection against unauthorised access | ➖/🟡 | Local desktop app; cloud auth handled by the OneWare.AI extension / OneWare Cloud accounts. | Document trust boundaries for extensions and cloud sign-in. |
| 4 | Confidentiality — encryption in transit & at rest | 🟡 | HTTPS for update/marketplace/cloud calls. Local project data stored per-OS. | Document TLS usage; note any stored credentials/tokens and their protection. |
| 5 | Integrity — signed updates, tamper protection | 🟡 | OneWare Studio is code-signed on **Windows** and **macOS**; **Linux builds are not signed**. Snap/Flathub provide their own store signing. | Sign Linux builds (or document reliance on Snap/Flathub store integrity). |
| 6 | Data minimisation / privacy by design | ❓ | Telemetry/analytics scope to confirm. | Document what data (if any) is collected and minimise; align with GDPR. |
| 7 | Availability / resilience | ➖ | Desktop application; not a networked service. | N/A beyond crash-resilience. |
| 8 | Limited negative impact on other devices/networks | 🟡 | Extensions can run tools/processes. | Document the extension permission/trust model. |
| 9 | Exploit mitigation mechanisms | ✅ | Managed .NET runtime provides ASLR/DEP/CFG and memory safety. | Note in technical docs. |
| 10 | Security information transparency | 🟡 | [../../SECURITY.md](../../SECURITY.md) now published. | Add user-facing security & update guidance to docs. |

## Annex I — Part II: Vulnerability Handling

| # | Requirement | Status | Notes / Evidence | Action |
|---|---|---|---|---|
| 1 | Identify & document vulnerabilities (incl. third-party) | 🟡 | SBOM (`.github/workflows/sbom.yml`) + Dependabot + `--vulnerable` gate. | Extend to submodules/native tools; triage cadence. |
| 2 | Address without delay — free updates | 🟡 | Regular releases across channels. SLAs adopted (7/30/90 days). | Track SLA adherence. |
| 3 | Coordinated Vulnerability Disclosure policy | ✅ | [../../SECURITY.md](../../SECURITY.md). | Keep current. |
| 4 | Public point of contact | ✅ | `info@one-ware.com`. | Ensure triage. |
| 5 | Share info with CERT/CSIRT networks | 🟡 | Reporter = CTO; CSIRT = BSI/CERT-Bund. | Confirm ENISA channel. |
| 6 | Provide SBOM on request | 🟡 | CycloneDX SBOM in CI. | Publish per release; extend coverage. |
| 7 | Free security updates for support period | 🟡 | 5-year commitment ([support-period.md](support-period.md)). | Communicate to users. |
| 8 | Report actively exploited vulns to ENISA/CSIRT (24 h / 72 h) | 🟡 | Runbook drafted ([vulnerability-handling-policy.md](vulnerability-handling-policy.md)). | Rehearse before 11 Sep 2026. |
| 9 | Notify users of issues & remediation | 🟡 | Release notes / in-app update. | Define a security-advisory channel. |

## Priority Actions

1. **Resolve the manufacturer-vs-steward determination** ([classification.md](classification.md) §1).
2. **Sign Linux builds** (or formally document Snap/Flathub store signing) — Part I #5.
3. **Document telemetry & data minimisation** — Part I #6.
4. **Make the dependency-scan a required release gate** — Part I #1.
