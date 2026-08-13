# 0001 — Clean Architecture with CQRS

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

BudgetGuard AI is an explainable anomaly detection platform for public
procurement. Its value proposition is not that it finds anomalies — plenty of
tools do — but that every finding can be verified by hand by the auditor acting
on it. That makes the statistical core the product, and its correctness the
thing most worth protecting.

The build has a hard 48-hour deadline and one developer, so architecture that
costs more than it returns inside that window is a real risk. Two failure modes
were both plausible:

1. A prototype script that works for the demo, cannot be tested, and cannot be
   extended after the competition.
2. Ceremonial layering — interfaces over interfaces — that consumes the
   deadline without improving anything.

The detection logic itself is the part most likely to be wrong in subtle,
invisible ways. Statistical bugs do not throw exceptions. A detector with a
misapplied threshold still returns a confident, plausible-looking ranked list.

## Decision

Adopt Clean Architecture with four layers — Domain, Application,
Infrastructure, Presentation — and CQRS via MediatR in the Application layer.

Specifically:

- `BudgetGuard.Domain` has **zero package references**. The detection
  algorithms are pure functions over plain types and settings records.
- Application declares the interfaces it needs (`IDatasetRepository`,
  `IDatasetFileParser`); Infrastructure implements them.
- Every use case is a MediatR command or query with its own handler and DTO.
- FluentValidation runs as a pipeline behaviour, not inside handlers.
- No business logic in API endpoints or Blazor components.

CQRS here means the handler pattern and separate command/query models. It does
**not** mean event sourcing, a separate read store, or eventual consistency —
those would be cost without benefit at this scale.

## Consequences

### What this bought

The domain layer's isolation is what made the statistical core testable at all.
112 domain tests construct populations in memory and assert on results with no
host, no database and no mocking framework; the suite runs in roughly 60
milliseconds. That speed is what made it practical to write tests asserting the
detectors stay *silent* on clean data — and those tests found three real
defects that a demo would have hidden:

- Raw-scale z-scores produced 55 false positives on a clean 1,200-row ledger,
  because spend distributions are heavily right-skewed.
- Benford's published MAD bands are invalid at per-vendor sample sizes; at
  n = 100 sampling noise alone exceeds the non-conformity threshold.
- Vendor concentration flagged any vendor holding one very large payment,
  conflating an amount outlier with market capture.

Each fix was confined to the domain layer. No infrastructure or UI changed.

The MediatR seam also removed a whole class of work: the Blazor front end and
the Web API are two surfaces over the same handlers, so there is no second
implementation of anything and no risk of the two disagreeing.

### What it cost

More files and more indirection than a single-project solution. Finding where
something happens requires knowing the layer convention. For a solo 48-hour
build this is a real, non-trivial tax — accepted because the alternative was
untestable statistics.

### What we are locked into

Adding a use case means touching several projects: a request, a handler, a DTO,
a validator, and an endpoint or component. That is deliberate friction, but it
is friction.

The domain's zero-dependency rule is load-bearing. If a future contributor adds
a package reference to `BudgetGuard.Domain` for convenience, the property that
makes the test suite fast and the algorithms verifiable quietly disappears. The
constraint is documented in the csproj itself and in
[CODING_STANDARDS.md](../CODING_STANDARDS.md).

## Alternatives considered

**Single-project vertical slices.** Faster to start and defensible for a
prototype. Rejected because the detection logic would end up entangled with EF
Core and ASP.NET, and the clean-data regression tests — the ones that found
every real bug — would have needed a host and a database to run.

**Traditional N-tier with service classes.** Would have worked. Rejected mainly
because "service" classes accumulate unrelated methods and the no-logic-in-
controllers rule becomes a matter of discipline rather than structure. With one
handler per use case there is nowhere for logic to leak to.

**Full CQRS with separate read/write stores.** Rejected outright. There is one
write path and no scale problem; the complexity would be pure cost.
