# 0002 — SQLite rather than PostgreSQL

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

BudgetGuard AI needs to persist uploaded datasets and their transactions. The
deployment target is a free-tier container platform, reached from a GitHub
repository, with a public HTTPS URL required inside 48 hours.

The obvious "serious" choice is PostgreSQL. It is worth being precise about why
it is not the right one here, because "we used SQLite" reads as a shortcut, and
in this case it genuinely is not.

The workload has an unusual shape:

- **Writes are rare and coarse.** One insert of a few thousand rows per upload.
  There is no update path at all; datasets are immutable once ingested.
- **Reads are whole-population.** Every statistic the product computes — a
  Benford distribution, a peer group's standard deviation, a vendor's share of
  spend — is a property of the entire dataset. Analysis loads all of a
  dataset's transactions and computes in memory. There are no filtered queries,
  no joins, no aggregation pushed into SQL.
- **Concurrency is low.** A demo instance and a single-agency pilot both mean a
  handful of concurrent users.

Given that, essentially nothing PostgreSQL is good at is being asked for. The
database is a durable row store, and the query engine is unused.

## Decision

Use SQLite via EF Core, with the database as a file inside the container, and
apply EF migrations at startup.

## Consequences

### Why this is the right call, not a shortcut

**The query engine is genuinely not needed.** The detection engine deliberately
computes in memory over the full population, because that is what the statistics
require. Choosing a database for query power that is never exercised would be
paying complexity for nothing.

**Zero operational surface.** No connection pooling, no credentials to manage or
leak, no second service to provision, no network hop, no separate free-tier
account with its own expiry. On a 48-hour clock this removed an entire category
of ways to lose an afternoon.

**Deployment is one container.** The app deploys from a Dockerfile with no
external dependency. That is a large part of why the deployment story is a
blueprint file and a button rather than a runbook.

**Decimal storage is a real advantage here.** The SQLite provider stores
`decimal` as text, preserving exact digits. Benford analysis reads the literal
leading digit of an amount, so a value round-tripped through a binary floating
point type could come back with a *different first digit than it went in with* —
silently corrupting the primary detector. Text storage makes that class of bug
impossible.

### What it costs

**No persistent disk on the free tier.** `/app/data` is ephemeral, so the
database resets whenever the instance restarts. This is mitigated rather than
solved: the synthetic demo dataset regenerates automatically on startup
(`Demo:SeedOnStartup`), so a reviewer always finds a working tool. Any
deployment holding real procurement records must attach a persistent volume and
set that flag to false. Both platform configs document this at the point of use.

**Single writer.** SQLite serialises writers with file locks. This is why
`docker-compose.yml` gives the Web and API containers separate volumes rather
than sharing one file — they are alternative front ends over the same engine,
not two halves of one system, so there is nothing to gain from sharing and lock
contention to lose.

**Concurrency ceiling.** Fine for a demo and a single-agency pilot. A
multi-tenant national deployment would need a real server database.

### Migration path

The cost of being wrong is low, and that is part of why the decision is
comfortable. EF Core abstracts the provider; moving to PostgreSQL means changing
`UseSqlite` to `UseNpgsql`, regenerating migrations, and reviewing two
provider-specific mapping decisions that are already commented in
`BudgetGuardDbContext`:

- `DateTimeOffset` is stored as UTC ticks, because SQLite cannot `ORDER BY` a
  `DateTimeOffset`. PostgreSQL can, so this conversion should be removed.
- The `decimal`-as-text behaviour is provider default here; a provider mapping
  to a binary type would need its Benford implications reconsidered.

Neither the domain nor the application layer changes.

## Alternatives considered

**PostgreSQL on a free tier (Neon, Supabase, Render).** Rejected for this stage.
It adds a second service, credentials, a network hop and another account with
its own free-tier expiry, in exchange for query capabilities the product does
not use. It remains the right choice the moment concurrent writers or
multi-tenancy appear.

**In-memory only.** Rejected: uploaded datasets would not survive a restart, and
a reviewer returning to a link would find their upload gone.

**LiteDB or a document store.** Rejected: the data is rectangular and relational,
EF Core support is weaker, and nothing is gained.
