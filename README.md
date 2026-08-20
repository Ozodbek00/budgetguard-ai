# BudgetGuard AI

**Explainable statistical forensics for government procurement and budget
spending.** Upload a spending dataset; get back a ranked list of anomalies where
every flag comes with the arithmetic behind it, in a sentence an auditor can
verify by hand.

Built for **Uzbekistan's President AI Award — Government AI track**.

### ▶ Live demo: **<https://budgetguard-ai.proread.uz>**
### ▶ API and Swagger: **<https://budgetguard-api.proread.uz>**

Available in **English, O'zbekcha and Русский** — the findings themselves are
translated, not just the interface.

Loaded with a synthetic dataset that has four fraud patterns planted in it.
Start on the [anomaly report](https://budgetguard-ai.proread.uz/report), then
see [why each flag was raised](https://budgetguard-ai.proread.uz/how-it-works).

The two hostnames are the same engine behind different bindings — the Blazor
app dispatches the same MediatR handlers in-process that the API exposes over
HTTP. They run as separate containers with separate databases, so an upload to
one is not visible in the other.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-221%20passing-2ea44f)](tests/)
[![Architecture](https://img.shields.io/badge/architecture-Clean%20%2B%20CQRS-1c5d99)](docs/ARCHITECTURE.md)

---

## The problem

Public procurement oversight runs on two unsatisfying options.

**Manual audit** is trusted but does not scale — a reviewer sampling 200 of
50,000 payments finds what is in the sample.

**Machine-learning anomaly detection** scales but is not trusted. It returns a
ranked list with no reviewable reasoning. An auditor cannot open an
investigation, and certainly cannot support a sanction, on the grounds that a
model scored something 0.87.

## The approach

Classical statistical forensics — the techniques are decades old and
court-tested — run automatically, with the reasoning exposed.

Instead of a score, BudgetGuard produces this:

> **"Alfa Qurilish Invest" received 31.1% of all spend in category
> "Construction" (16,446,676,399 of 52,946,940,210), versus an expected 5.3% if
> the 19 vendors competing in that category shared it evenly — 5.9x the
> even-split expectation. This is not a single large award: they hold 34 of 227
> contracts (15.0%) where an even award process would give them about 11.9, an
> excess of 6.6 standard deviations.**

Three independent detectors, each with a plain-language output:

| Method | What it catches |
|---|---|
| **Benford's Law** | Fabricated or manipulated amounts. Round-number invoicing; contracts priced just under an approval ceiling |
| **Peer-relative outliers** | Payments far outside the normal range *for their own vendor, category or department* |
| **Vendor concentration** | Suppliers capturing a category — bid rigging, tailored tenders, undisclosed relationships. Includes market-level HHI |

Signals are merged into one ranked list. Independent methods agreeing on the
same subject is treated as materially stronger evidence and surfaced as such.

## Screens

| | |
|---|---|
| **Upload** | Drag-and-drop CSV/Excel with schema feedback, or one-click demo data |
| **Anomaly report** | Ranked findings, expandable to each contributing signal and its raw statistics, filterable by category, department and severity |
| **Benford's Law** | Expected versus observed first-digit distribution, with the threshold that was actually applied and why |
| **Vendor risk** | Per-scope Herfindahl-Hirschman Index and a sortable table of every supplier's share |
| **How this works** | The full methodology, rendered from the same file as the published docs |

---

## Quickstart

```bash
git clone https://github.com/Ozodbek00/budgetguard-ai.git
cd budgetguard-ai
dotnet run --project src/BudgetGuard.Web
```

Open <http://localhost:5000>. The demo dataset is generated automatically on
first run, so there is something to look at immediately.

With Docker:

```bash
docker compose up --build
```

UI on <http://localhost:8080>, Swagger on <http://localhost:8081/swagger>.

Run the test suite:

```bash
dotnet test
```

---

## What makes this trustworthy

The engineering claim of this project is not that it detects anomalies. It is
that it **does not cry wolf**, and that it can show its work.

Three genuine statistical defects were found during the build — every one of
them by testing the detectors against *clean* data and asserting silence, not by
testing that planted fraud was caught:

1. **Raw-amount z-scores over-flag skewed spending.** Procurement amounts are
   heavily right-skewed, so the top of any realistic distribution sits beyond
   three standard deviations by construction. A clean 1,200-row ledger produced
   **55 false positives**. Comparing on a log scale produces **zero**, while
   still catching genuine order-of-magnitude outliers.

2. **Benford's published MAD bands are invalid at small sample sizes.** At
   n = 100, sampling noise alone yields MAD of about 0.024 — above the 0.015
   non-conformity threshold — so a naive per-vendor test flags essentially every
   vendor. Deviations are now judged against a sample-size-aware noise floor of
   0.2348/sqrt(n).

3. **Vendor concentration conflated one big contract with market capture.** A
   single enormous payment gives its vendor a large share of scope spend by
   definition, so every amount outlier also branded its vendor as dominant. A
   flag now additionally requires a statistically significant excess of contract
   *count* against Binomial(T, 1/N).

Measured behaviour on the 1,710-row demo dataset:

| | |
|---|---|
| Findings total | 10 |
| Critical | 2 — both deliberately planted vendors |
| Corroborated by 2+ independent methods | 1 |
| Planted anomalies detected | 4 of 4 |
| Actionable findings as a share of the ledger | 0.6% |

The clean-ledger regression test asserts **zero** High or Critical findings on
1,200 rows of honest synthetic spending.

Full reasoning, thresholds and limitations:
**[docs/DETECTION_METHODOLOGY.md](docs/DETECTION_METHODOLOGY.md)**.

---

## Architecture

Clean Architecture with CQRS. The dependency arrow points inward:

```
BudgetGuard.Domain          detection algorithms - ZERO framework dependencies
       ^
BudgetGuard.Application     MediatR commands/queries, DTOs, FluentValidation
       ^
BudgetGuard.Infrastructure  EF Core / SQLite, CSV + Excel parsing
       ^
BudgetGuard.Api  ·  BudgetGuard.Web    two surfaces over one engine
```

The domain project has **no package references at all**. That is not
decoration — it is why 151 detection tests run in about 90 milliseconds with no
host, no database and no mocking framework, and therefore why it was practical
to write the clean-data regression tests that found all three defects above.

The Blazor app does not call the API over HTTP. Both front ends compose the same
Application services and dispatch the same MediatR messages, so there is exactly
one implementation of every use case.

## Documentation

| | |
|---|---|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layer responsibilities, request flows, why Clean Architecture and CQRS here |
| [DETECTION_METHODOLOGY.md](docs/DETECTION_METHODOLOGY.md) | The statistics: theory, thresholds, corrections, limitations, validation |
| [DATA_MODEL.md](docs/DATA_MODEL.md) | Entities, input schema, accepted column names, parsing rules |
| [CODING_STANDARDS.md](docs/CODING_STANDARDS.md) | Handler patterns, error handling, localisation, the five load-bearing rules |
| [CONTRIBUTING.md](docs/CONTRIBUTING.md) | Local setup, tests, deployment steps, adding a detection rule |
| [UZBEK_DOMAIN_REFERENCE.md](docs/UZBEK_DOMAIN_REFERENCE.md) | Regions, vendor naming, amount calibration — and what is assumed rather than verified |
| [ADR 0001](docs/adr/0001-clean-architecture-cqrs.md) | Clean Architecture + CQRS |
| [ADR 0002](docs/adr/0002-sqlite-over-postgres-for-mvp.md) | SQLite rather than PostgreSQL |
| [ADR 0003](docs/adr/0003-blazor-server-over-spa.md) | Blazor Server rather than a SPA |

## Input format

CSV or Excel. Required columns: `TransactionDate`, `Amount`, `VendorName`,
`Category`, `Department`. Optional: `ExternalReference`, `Currency`,
`Description`.

Common alternative header names (`Supplier`, `Ministry`, `ContractValue`,
`ProcuringEntity`, and others) are recognised automatically, and both
`1.234,56` and `1,234.56` parse correctly. Full details in
[DATA_MODEL.md](docs/DATA_MODEL.md).

## Deployment

The live instance runs on a Hetzner VPS behind an existing Caddy that already
serves other sites: the stack publishes no host ports, joins that proxy's Docker
network, and gets TLS for one more hostname. Uploaded data lives on a named
volume and survives redeploys.

`render.yaml` and `fly.toml` are also committed, and the container binds
`$PORT`, so the same image runs unchanged on Render, Fly.io or Cloud Run. All
three paths are in [CONTRIBUTING.md](docs/CONTRIBUTING.md#deploying).

---

## Demo data is synthetic

The built-in demo dataset is **generated and entirely fictional**. Vendor names,
departments and amounts are invented, and four fraud patterns are planted in it
deliberately so the detectors can be scored against ground truth. It is labelled
in the database, in the dataset name, and with a banner on every screen that
displays it.

**It is not real government procurement data and must never be presented as
such.** No real supplier or agency is described anywhere in this repository.

## Scope and limitations

BudgetGuard AI produces **screening leads for human review**. Nothing it outputs
is a determination of wrongdoing. Legitimate concentration, legitimate large
contracts and legitimately non-Benford data all exist, and the known
false-positive risks are documented rather than hidden — see the limitations
section of
[DETECTION_METHODOLOGY.md](docs/DETECTION_METHODOLOGY.md#known-limitations-and-false-positive-risks).

The single largest evasion route is deliberate splitting of a supplier across
related legal entities, which name-based matching cannot see. Addressing it
needs a company registry integration.

## Licence

MIT — see [LICENSE](LICENSE).
