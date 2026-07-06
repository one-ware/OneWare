# CRA Scope & Classification — OneWare Studio

> Workflow 1 — Product Classification and Scope Assessment.
> **Status: DRAFT — the open-source determination in §1 needs legal sign-off.**

**Manufacturer / steward:** ONE WARE GmbH, Annengasse 3, 33034 Brakel, Germany —
Phone +49 1522 6837464 — `info@one-ware.com`.

**Product:** OneWare Studio — a .NET 10 / Avalonia desktop IDE for electronics
development (VHDL, Verilog, C++, FPGA), distributed **free of charge** under
**GPL-3.0** via direct download, Snap, Flathub and WinGet, plus a web build.

## 1. Scope & the Open-Source Question

OneWare Studio has network interfaces (Git integration, extension marketplace,
update checks, and cloud connectivity through the OneWare.AI extension), so it is
a **product with digital elements** by nature.

The decisive CRA question for open source is **whether it is supplied "in the
course of a commercial activity"**:

- The CRA **excludes** free and open-source software that is **not** monetised or
  supplied commercially, and provides a lighter **"open-source software steward"**
  regime for entities that provide sustained support for OSS placed on the market
  commercially.
- OneWare Studio is developed by a commercial company (ONE WARE GmbH) and acts as
  the **host/gateway** for the commercial **OneWare.AI** extension and OneWare
  Cloud offering (indirect monetisation).

**Two possible positions (decision required):**

| Position | Consequence |
|---|---|
| **(A) Manufacturer** (conservative) | Full Annex I obligations, technical documentation, DoC, CE marking for the Studio product. |
| **(B) Open-source steward** | Lighter regime: cybersecurity policy, coordinated disclosure, cooperation with authorities, forward vulnerability info downstream — **no** full conformity assessment or CE marking. |

**Recommendation:** Proceed with the **steward baseline** (a published security
policy + vulnerability handling, already in this repo) while obtaining **legal
confirmation**. If OneWare Studio is deemed commercially placed on the market,
escalate to the manufacturer position (complete
[technical-documentation.md](technical-documentation.md) and the
[DoC](declaration-of-conformity-template.md)).

**Exclusions checked:** Not medical/aviation/automotive/marine/military. None apply.

## 2. Product Class (if treated as manufacturer)

**Default** → Module A self-assessment. OneWare Studio is a general development /
EDA IDE. The product owner has confirmed it is **not** intended for
safety-critical software builds, so the Annex III (Class I) "compilers/build tools
for safety-critical software" category does **not** apply. No Annex IV (Class II)
category applies.

## 3. Organisation Role

**ONE WARE GmbH** — manufacturer or open-source steward per §1. EU-established, so
**no authorised representative required**.

## Key Obligations (steward baseline, already actionable)

- Cybersecurity / vulnerability-disclosure policy — [../../SECURITY.md](../../SECURITY.md).
- Vulnerability handling & coordinated disclosure — [vulnerability-handling-policy.md](vulnerability-handling-policy.md).
- Cooperate with market-surveillance authorities and forward vulnerability info downstream.
- SBOM available on request — automated via `.github/workflows/sbom.yml`.
- (If manufacturer) technical documentation, DoC, CE marking, 5-year support, ENISA/CSIRT reporting.
