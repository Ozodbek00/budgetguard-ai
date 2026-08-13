# Architecture

## System overview

BudgetGuard AI ingests a procurement or budget spending dataset, runs three
independent statistical detectors over it, and returns a ranked list of
findings — each carrying the arithmetic that produced it.

```
                 ┌──────────────────────────────┐
   CSV / XLSX ──▶│  BudgetGuard.Web  (Blazor)   │──┐
                 └──────────────────────────────┘  │
                                                   │  MediatR (in-process)
                 ┌──────────────────────────────┐  │
   HTTP client ─▶│  BudgetGuard.Api  (Swagger)  │──┤
                 └──────────────────────────────┘  │
                                                   ▼
                            ┌────────────────────────────────────┐
                            │  BudgetGuard.Application            │
                            │  commands, queries, DTOs, validation│
                            └────────────────────────────────────┘
                               │                            │
                               ▼                            ▼
        ┌──────────────────────────────┐   ┌────────────────────────────────┐
        │  BudgetGuard.Domain          │   │  BudgetGuard.Infrastructure    │
        │  detection algorithms        │   │  EF Core / SQLite, CSV, XLSX   │
        │  entities, settings          │   │  repositories                  │
        │  ZERO framework dependencies │   └────────────────────────────────┘
        └──────────────────────────────┘
```

## Layer responsibilities

### `BudgetGuard.Domain`

The detection algorithms and the entities they operate on. `BenfordAnalyzer`,
`ZScoreOutlierDetector`, `VendorConcentrationAnalyzer` and `AnomalyAggregator`
live here as pure domain services: deterministic functions from transactions and
settings to findings.

**This project has zero `PackageReference` entries, and that is enforced by
inspection of the csproj.** No EF Core, no ASP.NET, no MediatR. The practical
payoff is the test suite — 112 tests construct populations in memory and assert
on results with no host, no database and no mocking framework, which is why they
run in about 60 milliseconds and why it is cheap to test the statistical edge
cases that actually matter.

`DetectionSettings` also lives here. The domain declares what is configurable;
the composition root binds it from `appsettings.json`.

The synthetic data generator is here too. It is domain-shaped code — pure
functions producing entities — and putting it here means the CLI tool, the
"Load demo dataset" button and the test suite all exercise one implementation.

### `BudgetGuard.Application`

CQRS via MediatR. One command or query per use case, each with its own request,
response DTO and handler.

- `UploadDatasetCommand`, `LoadDemoDatasetCommand`
- `GetAnomalyReportQuery`, `GetBenfordDistributionQuery`, `GetVendorRiskQuery`,
  `GetDatasetsQuery`

FluentValidation runs as a MediatR pipeline behaviour, so a handler can assume
its request is valid and no caller can skip validation by forgetting to invoke
it. Adding a validator class enforces a rule across every surface at once.

Interfaces the application needs from the outside world (`IDatasetRepository`,
`IDatasetFileParser`) are declared here and implemented in Infrastructure. This
is the dependency inversion that keeps the arrow pointing inward.

`AnalysisService` loads a dataset, runs the pipeline and caches the result.
Datasets are immutable once uploaded, so caching is not a consistency risk — and
it matters because the report, the Benford chart and the vendor risk view are
three views of a single detection run.

### `BudgetGuard.Infrastructure`

EF Core over SQLite, repositories, and CSV/Excel parsing. Both file formats are
reduced to the same header-plus-rows shape and run through one mapping routine,
so a CSV and an equivalent `.xlsx` cannot produce different anomaly reports from
the same numbers.

### `BudgetGuard.Api` and `BudgetGuard.Web`

Two front ends over the same engine. Both compose the same services and dispatch
the same MediatR messages. Neither contains business logic; endpoints and
components bind input, send a message, and render the result.

They are **alternative surfaces, not tiers**. The Blazor app does not call the
API over HTTP — it goes straight to the Application layer in-process. See
[ADR 0003](adr/0003-blazor-server-over-spa.md).

## Why Clean Architecture here

The usual argument is swappability of infrastructure. That is not the real
motivation on this project.

The motivation is that **correctness of the detection logic is the product**.
A flag an auditor cannot verify is worthless, and a detector that quietly
regresses is worse than no detector. The layering exists so the statistical core
can be exercised exhaustively, in isolation, with no infrastructure in the way.

That paid off concretely. Three genuine statistical defects — raw-scale z-scores
over-flagging heavy-tailed spend, Benford's MAD bands being invalid at small
sample sizes, and vendor concentration conflating one large award with market
capture — were all found by running the detectors against *clean* synthetic data
and asserting silence. Each was a change confined to the domain layer, with no
infrastructure or UI churn.

## Why CQRS

Reads and writes here are genuinely asymmetric. There is exactly one meaningful
write — ingest a dataset — and several substantial reads, each shaping the same
detection run into a different projection.

Modelling those as separate query handlers means each view owns its own DTO and
can evolve without disturbing the others, and it keeps the "no business logic in
controllers or components" rule mechanically easy to follow: there is nowhere
for logic to leak, because the endpoint's whole body is one `Send` call.

No event sourcing, no separate read database, no eventual consistency. CQRS here
means the handler pattern and the separation of command and query models, and
nothing more.

## Request flow — an upload

1. Blazor `InputFile` or `POST /api/datasets` receives the file.
2. `UploadDatasetCommand` is dispatched.
3. `ValidationBehaviour` runs `UploadDatasetCommandValidator` (extension, size).
4. `UploadDatasetCommandHandler` calls `IDatasetFileParser`.
5. The parser maps headers tolerantly, converts rows, collects per-row problems.
6. Schema failures become a `ValidationException` → RFC 7807 or inline UI errors.
7. Accepted transactions are persisted via `IDatasetRepository`.
8. The client is redirected to the report for the new dataset.

## Request flow — a report

1. `GetAnomalyReportQuery` is dispatched.
2. `AnalysisService` returns a cached run, or loads transactions and runs
   `AnomalyAggregator`.
3. The aggregator runs all three detectors, including per-vendor Benford, and
   merges signals by subject.
4. The handler maps domain findings to DTOs and applies display filters.

Filters are applied **after** detection, never before. Excluding a category from
the population first would change every peer group, every vendor share and the
Benford baseline, so a filtered view's numbers would no longer match the
unfiltered ones. The auditor filters what they are looking at, not what the
engine analysed.

## Testing strategy

| Project | Scope |
|---|---|
| `BudgetGuard.Domain.Tests` | The detection algorithms. Constructed populations with known properties; every threshold and guard rail; ground-truth scoring against the synthetic generator. |
| `BudgetGuard.Application.Tests` | Handler wiring, validation, and file parsing against real CSV and XLSX content. |

Randomness in tests is always seeded. A detection test that passes
intermittently is worse than no test.

## Data flow constraints worth knowing

- Analysis loads a dataset's transactions **in full**. Every statistic is a
  property of the whole population, so there is nothing to stream or paginate.
  This bounds practical dataset size to what fits comfortably in memory —
  appropriate at demo and single-agency scale, and the first thing to revisit
  for national-scale data.
- Amounts are `decimal` end to end, and SQLite stores them as text. Benford
  analysis reads literal leading digits, so a value round-tripped through binary
  floating point could come back with a different first digit than it went in
  with.
