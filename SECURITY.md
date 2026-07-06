# Security Policy

ONE WARE GmbH takes the security of OneWare Studio seriously. This document is
our **Coordinated Vulnerability Disclosure (CVD) policy** and public point of
contact for reporting security vulnerabilities, aligned with the EU Cyber
Resilience Act (Regulation (EU) 2024/2847), Annex I Part II.

OneWare Studio is free and open-source software (Apache-2.0). See
[compliance/cra/](compliance/cra/README.md) for the full CRA compliance
documentation, including the open-source classification analysis.

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions, or pull requests.**

Report them privately by email:

- **Email:** `info@one-ware.com` (please put *"Security"* in the subject line)

Please include, where possible:

- The affected component and version / commit / installed package (Snap, Flathub,
  WinGet, or direct download).
- Your operating system and how OneWare Studio was installed.
- Step-by-step reproduction instructions or a proof of concept.
- The potential impact.
- Any suggested remediation.

## Our Commitment (Response Targets)

| Stage | Target |
|---|---|
| Acknowledge receipt | within **3 business days** |
| Initial assessment & severity triage (CVSS) | within **10 business days** |
| Status updates during remediation | at least **every 14 days** |
| Fix or documented mitigation (by severity) | Critical 7 days · High 30 days · Medium 90 days |

We follow **coordinated disclosure** and will credit reporters who wish to be
acknowledged.

## Scope

In scope:

- OneWare Studio desktop application (Windows, macOS, Linux) and the web build.
- Source code in this repository and first-party OneWare extensions maintained here.

Out of scope:

- Third-party extensions and integrated tools (GHDL, IVerilog, etc.) — report to
  the respective project.
- The closed-source OneWare.AI extension and OneWare Cloud — see their own
  security policies / `info@one-ware.com`.
- Social engineering, physical attacks, or automated-scanner output without a
  demonstrated, exploitable impact.

## Supported Versions & Security Updates

Security updates are provided free of charge for supported versions throughout
the product support period. See
[compliance/cra/support-period.md](compliance/cra/support-period.md).

## Regulatory Reporting

For **actively exploited vulnerabilities** and **severe incidents**, ONE WARE
GmbH notifies ENISA and the German national CSIRT (BSI/CERT-Bund) within the
CRA timelines (early warning within 24 hours, full notification within 72 hours).
See [compliance/cra/vulnerability-handling-policy.md](compliance/cra/vulnerability-handling-policy.md).
