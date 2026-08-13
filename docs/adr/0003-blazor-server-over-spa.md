# 0003 — Blazor Server rather than a SPA

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

The product needs five screens: upload, ranked anomaly report, Benford
distribution chart, vendor risk table, and a methodology page. One developer,
48 hours, and a live public URL required as early as possible.

The screens are, in UI terms, unremarkable: forms, tables, filters, one chart.
What is *not* unremarkable is the payload behind them. A single finding carries
a severity, a risk score, several contributing signals, each with a paragraph of
explanation and a dictionary of raw statistics. The interesting work is
statistical, not interactive.

The realistic options were a React/Angular SPA against the Web API, Blazor
WebAssembly, or Blazor Server.

## Decision

Use Blazor Server, hosted in `BudgetGuard.Web`, composing the Application layer
directly and dispatching MediatR messages **in-process**.

The Blazor app does not call `BudgetGuard.Api` over HTTP. The two are
alternative front ends over the same engine, not tiers of one system.

## Consequences

### What this bought

**One language, one toolchain, one deployment.** No separate `npm` build, no
bundler configuration, no second container, no CORS setup, no API client to
generate or hand-write. On this timeline that is the difference between shipping
five screens and shipping two.

**No DTO drift and no duplicated logic.** Because the UI dispatches the same
commands the API exposes, there is exactly one implementation of every use case.
A SPA would have required a second representation of every DTO in TypeScript and
a client layer to keep in sync — pure duplication for a tool whose screens are
tables.

**Server-side rendering of the substance.** The full report, including every
explanation, is present in the server-rendered HTML. That matters more here than
it usually would: an auditor can print or archive a finding with its reasoning
intact, and the pages are inspectable without executing JavaScript.

**The chart came free.** The Benford chart is inline SVG generated server-side.
No charting library, no CDN dependency to be blocked by a content-security
policy, no JS interop that must be deferred past prerendering and would leave
the chart blank on first paint.

**The methodology stays in sync.** The "How this works" page renders
`docs/DETECTION_METHODOLOGY.md` directly. The published methodology and the
in-product explanation are one file, which would have been awkward across a
client/server split.

### What it costs

**A stateful WebSocket per user.** Every connected client holds a circuit on the
server. Memory scales with concurrent users, and this is the real ceiling on the
approach. Acceptable for a demo and a single-agency pilot; not for a public
portal with thousands of simultaneous sessions.

**Latency sensitivity.** Every interaction round-trips. Filter changes on the
report feel immediate on a local or regional deployment and would feel sluggish
to a user on a poor connection to a distant region. The app is deployed in
Frankfurt partly for this reason.

**No offline capability**, and a dropped connection interrupts the session.
Fly.io's `auto_stop_machines` is configured with generous connection limits so a
reviewer reading a long report is not disconnected by a scale-to-zero.

**Ties the UI to .NET hosting.** A future public-facing portal would likely want
a SPA. That path stays open precisely because `BudgetGuard.Api` already exposes
the full functionality over HTTP with Swagger — a SPA can be built against it
without touching the Application layer.

### Why the API still exists

It would have been defensible to drop it. It is kept because it is genuinely
useful rather than decorative: it gives programmatic access for integration into
an existing audit workflow, it makes the engine demonstrable and inspectable via
Swagger without the UI, and it keeps the option of a different front end open at
no ongoing cost — it is the same handlers behind a different binding.

## Alternatives considered

**React or Angular SPA against the Web API.** The conventional choice, and the
right one for a large public portal. Rejected here: it doubles the deployment,
introduces CORS and an API client, and requires re-modelling every DTO in
TypeScript — significant cost for screens that are tables, on a 48-hour clock.

**Blazor WebAssembly.** Keeps C# end to end and scales without server circuits,
but ships a multi-megabyte runtime, has a slow first load, and would still need
the API plus CORS. It also could not render the methodology markdown from the
repository or produce server-rendered findings.

**Razor Pages / MVC.** Lighter than Blazor Server and would have worked. Rejected
because the report and vendor tables want interactive filtering and sorting, and
doing that with full page reloads or hand-written JavaScript would have been
more work, not less.
