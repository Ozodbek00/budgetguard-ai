# Coding standards

Project-specific rules. General C# conventions follow the .NET runtime team's
guidelines; what follows is what is particular to BudgetGuard AI, and why.

---

## The rules that are load-bearing

These four are not style preferences. Breaking any of them removes a property
the product depends on.

### 1. `BudgetGuard.Domain` has zero package references

No EF Core, no ASP.NET Core, no MediatR, no logging abstractions. The detection
algorithms are pure functions over plain types and settings records.

**Why:** this is what makes the statistical core testable in isolation. The 112
domain tests need no host, no database and no mocking framework, and run in
about 60 milliseconds. That speed is why it is practical to assert the detectors
stay *silent* on clean data — the assertions that found every real defect in
this codebase.

If you need something from a framework inside the domain, you need an
abstraction defined in the domain and implemented outside it.

### 2. No business logic in endpoints or components

An API endpoint's body binds input, sends a MediatR message, and returns the
result. A Blazor component does the same and renders it.

**Why:** the Web UI and the API are two surfaces over one engine. The moment
logic lives in one of them, the two can disagree about what the product does.

Presentation-only work — sorting an already-loaded table, formatting a number,
choosing a CSS class from a severity — belongs in the component. The test is
whether the API would need the same code to behave correctly. If yes, it belongs
in a handler.

### 3. Every detection threshold is configurable

No magic numbers in detector code. Every decision boundary lives in
`DetectionSettings` and is bound from the `Detection` section of
`appsettings.json`.

**Why:** an auditor tuning the tool for their jurisdiction must be able to see
and change every boundary, and a published finding must be reproducible from a
known settings set. A threshold buried in an `if` is neither.

Named constants for *mathematical* facts are fine and preferred — `0.6745` for
the MAD consistency constant, `0.2348` for the null-MAD coefficient. Those are
properties of the statistics, not policy choices.

### 4. Every signal carries an explanation with its arithmetic

A detector never emits a bare score. It emits a sentence naming the subject, the
measured value, the comparison population and the threshold applied — enough for
a reader to recompute it by hand.

**Why:** this is the product. A score without a sentence is exactly the black box
BudgetGuard exists to replace.

Compare:

```csharp
// No.
return new Signal(score: 0.87, reason: "Unusual vendor concentration");

// Yes.
return $"\"{vendor}\" received {Percent(share)} of all spend in category " +
       $"\"{scope}\" ({spend:N0} of {total:N0}), versus an expected " +
       $"{Percent(expected)} if the {vendorCount} vendors competing in that " +
       $"category shared it evenly — {multiple:F1}x the even-split expectation.";
```

---

## CQRS handler pattern

One file per use case, holding the request, its validator, and its handler. They
change together; splitting them across three files adds navigation cost for no
benefit.

```csharp
public sealed record GetVendorRiskQuery(
    Guid? DatasetId = null,
    string? ScopeType = null,
    bool FlaggedOnly = false) : IRequest<VendorRiskDto>;

public sealed class GetVendorRiskQueryHandler(IAnalysisService analysisService)
    : IRequestHandler<GetVendorRiskQuery, VendorRiskDto>
{
    public async Task<VendorRiskDto> Handle(
        GetVendorRiskQuery request,
        CancellationToken cancellationToken)
    {
        ...
    }
}
```

Conventions:

- Requests are `sealed record`s named `<Verb><Noun>Command` or `<Verb><Noun>Query`.
- Handlers are `sealed class`es named `<RequestName>Handler`.
- Handlers use primary constructors for injection.
- One handler, one responsibility. If a handler needs a second dependency to do
  unrelated work, it is probably two use cases.
- Handlers never return domain entities. They return DTOs, so the transport
  contract can change without touching the domain.

## Validation

FluentValidation validators live beside their command and run as a pipeline
behaviour. Never call a validator from inside a handler — a handler may assume
its request is already valid.

Validate the *request*; let the domain enforce its own invariants. Whether a
filename ends in `.csv` is a request concern. Whether a peer group is large
enough to support a flag is a domain concern.

## Naming

| Thing | Convention | Example |
|---|---|---|
| Command / query | `<Verb><Noun>Command` / `Query` | `UploadDatasetCommand` |
| Handler | `<Request>Handler` | `UploadDatasetCommandHandler` |
| Validator | `<Request>Validator` | `UploadDatasetCommandValidator` |
| DTO | `<Noun>Dto` | `AnomalyFindingDto` |
| Domain service | `<Noun><Role>` | `VendorConcentrationAnalyzer` |
| Settings | `<Area>Settings` | `ZScoreSettings` |
| Test | `Method_does_the_expected_thing` | `Clean_ledger_produces_no_high_or_critical_findings` |

British or American spelling: the codebase uses `Analyzer` for detector class
names (matching .NET convention) and British spelling in prose and in local
identifiers like `NormalisedScore`. Match the file you are in.

## Error handling

Three categories, three treatments:

| Category | Mechanism | Surface |
|---|---|---|
| Invalid request | `ValidationException` from the pipeline | 400 + RFC 7807, or inline field errors |
| Missing entity | `NotFoundException` | 404, or an empty-state message |
| Everything else | Unhandled | 500, logged with full detail, nothing internal leaked |

`ApiExceptionHandler` maps only the exception types it can describe. Anything
else falls through deliberately rather than being reported with a misleading
status code.

**Do not swallow exceptions to make a screen render.** An empty table where an
error occurred is worse than an error message — in an audit tool it implies "no
anomalies found".

Row-level data problems are not exceptions. The file parser collects and reports
them, because partial success is the correct outcome for a 5,000-row file with
three bad dates.

## Comments

Comment the *why*, never the *what*. The bar: would a competent reader wonder
why this is like this?

XML doc comments on public domain service methods must explain the **statistical
reasoning**, not the mechanics. `ZScoreOutlierDetector` documents the masking
problem and why the log scale is the default; it does not document that it
computes a standard deviation.

Every non-obvious threshold, guard rail and workaround carries the reason it
exists. Several exist because of a specific bug — those say so.

## Testing

Detection tests are the product's warranty. They are held to a higher bar than
the rest.

- **Construct populations explicitly** so the expected result is derivable by
  hand. Where randomness is used it is **always seeded** — a detection test that
  passes intermittently is worse than no test.
- **Test that clean data is not flagged**, not only that planted anomalies are.
  Every genuine statistical defect in this codebase was found by a silence
  assertion, and none by a detection assertion.
- **Isolate the variable.** Detector tests configure a single grouping or scope,
  because with all three active one planted anomaly legitimately produces three
  findings and obscures what is being asserted.
- **Assert on explanation text**, not just scores. The sentence is the product,
  so a change that drops the threshold from an explanation should fail a test.
- **Score against ground truth.** The synthetic generator records what it
  planted; `SyntheticDatasetDetectionTests` asserts each planted anomaly is found
  *and* that the actionable queue stays a small fraction of the ledger.

## Adding a new detection rule

1. Add its settings to `DetectionSettings` with documented defaults.
2. Write the detector in `BudgetGuard.Domain/Detection/<Area>/` as a pure
   service with an interface. No framework dependencies.
3. Return findings that carry an explanation containing the arithmetic.
4. Write tests **before** wiring it up: planted-anomaly detection, clean-data
   silence, every guard rail, threshold configurability, explanation content.
5. Add a `DetectorKind` member and a weight in `ScoringSettings`.
6. Emit signals from `AnomalyAggregator`.
7. Register it in `Application/DependencyInjection.cs`.
8. Add the defaults to both `appsettings.json` files.
9. Document the method, its thresholds and its false-positive risks in
   `docs/DETECTION_METHODOLOGY.md` — which is rendered as the in-product
   "How this works" page, so this step is user-facing, not just paperwork.

Step 9 is not optional. A detector whose reasoning is not published is a black
box, which is the thing this product exists not to be.
