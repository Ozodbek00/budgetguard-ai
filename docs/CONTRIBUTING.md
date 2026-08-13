# Contributing

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) — `global.json`
  pins the 9.0 band with `rollForward: latestFeature`, so any 9.0.x SDK works.
- Docker (only for container builds and `docker compose`).

No database to install. SQLite is a file.

## Running locally

```bash
git clone https://github.com/Ozodbek00/budgetguard-ai.git
cd budgetguard-ai
dotnet restore
```

Run the Blazor UI — this is the whole product:

```bash
dotnet run --project src/BudgetGuard.Web
```

On first start it applies EF migrations and, if the database is empty, generates
the synthetic demo dataset, so the tool is populated immediately. Disable that
with `Demo__SeedOnStartup=false`.

Run the API with Swagger:

```bash
dotnet run --project src/BudgetGuard.Api
```

`/` redirects to `/swagger`. Both projects expose `/health`.

## Running the tests

```bash
dotnet test
```

Domain tests only, which is the suite that matters most and runs in well under a
second:

```bash
dotnet test tests/BudgetGuard.Domain.Tests
```

## Inspecting detector behaviour

The data generator doubles as a diagnostic. This prints the full ranked report
against ground truth, which is the fastest way to see what a threshold change
actually did:

```bash
dotnet run --project tools/BudgetGuard.DataGenerator -- diagnose
```

Write the demo dataset to CSV, for testing the upload path or as a schema
example:

```bash
dotnet run --project tools/BudgetGuard.DataGenerator -- csv demo-procurement-data.csv
```

## Docker

```bash
docker compose up --build
```

- Blazor UI — <http://localhost:8080>
- Swagger — <http://localhost:8081/swagger>

Or build and run one image directly. Note the build context is the repository
root, not the project directory, because the build needs `Directory.Build.props`
and the referenced projects:

```bash
docker build -f src/BudgetGuard.Web/Dockerfile -t budgetguard-web .
docker run -p 8080:8080 -e PORT=8080 budgetguard-web
```

The entrypoint binds `${PORT:-8080}`, which is what makes the same image work
unchanged on Render, Fly.io and Cloud Run.

---

## Deploying

### Render (recommended — fastest path to a public HTTPS URL)

`render.yaml` in the repository root is a Blueprint, so the deployment is
reproducible from version control rather than from dashboard clicks.

1. Sign in at <https://dashboard.render.com> (GitHub sign-in is fine).
2. **New → Blueprint**.
3. Connect the GitHub account and select the `budgetguard-ai` repository.
   Render detects `render.yaml` automatically.
4. Confirm. It reads the Docker build, provisions a free web service in
   Frankfurt, and health-checks `/health`.
5. First build takes roughly 4–6 minutes. The URL is
   `https://budgetguard-ai.onrender.com` (Render appends a suffix if the name
   is taken).

`autoDeploy: true` means pushes to `main` redeploy automatically.

**Free tier caveats, both handled:**

- **No persistent disk.** `/app/data` is ephemeral, so the database resets on
  restart. The demo dataset regenerates automatically on startup, so a reviewer
  always finds a working tool. For real data, attach a disk mounted at
  `/app/data` and set `Demo__SeedOnStartup=false`.
- **Spins down after ~15 minutes idle.** The first request after that takes
  30–60 seconds to cold-start. Worth warming the URL before a live demo.

### Self-hosted VPS behind an existing reverse proxy (current production)

This is how the live instance at <https://budgetguard-ai.proread.uz> runs: a
Hetzner box that already serves other sites behind a single Caddy container.

Ports 80 and 443 can only be bound once per host, so this stack ships no proxy
of its own. It publishes **no host ports at all** and joins the incumbent
proxy's Docker network, letting that proxy reach the app by container name and
terminate TLS for one more hostname. The app is therefore unreachable from the
internet except through the proxy.

**1. Point DNS at the server.** An `A` record for the hostname must resolve
before Caddy can obtain a certificate:

```
budgetguard-ai.proread.uz.   A   <server-ip>
```

**2. Clone and start the stack.**

```bash
git clone https://github.com/Ozodbek00/budgetguard-ai.git /opt/budgetguard-ai
cd /opt/budgetguard-ai
docker compose -f deploy/docker-compose.shared-proxy.yml up -d --build
```

Override `EDGE_NETWORK` if the existing proxy's network is not `profin_default`:

```bash
EDGE_NETWORK=my_proxy_net docker compose -f deploy/docker-compose.shared-proxy.yml up -d --build
```

**3. Add the site block to the existing Caddyfile.** Copy the block from
[`deploy/Caddyfile.shared`](../deploy/Caddyfile.shared) into the Caddyfile the
running proxy already uses. **Back it up and validate before reloading** — a
syntax error takes down every other site that Caddy serves, not just this one:

```bash
cp /opt/profin/deploy/Caddyfile /opt/profin/deploy/Caddyfile.bak
# ... append the block ...
docker exec profin-caddy-1 caddy validate --config /etc/caddy/Caddyfile
docker exec profin-caddy-1 caddy reload  --config /etc/caddy/Caddyfile
```

Caddy requests the certificate on the first HTTPS request to the new hostname.

**4. Verify.**

```bash
curl -s https://budgetguard-ai.proread.uz/health
curl -s https://budgetguard-api.proread.uz/health
```

The stack runs two containers: the Blazor app on one hostname and the API with
its Swagger UI on another. They keep separate databases — see the comment on the
`api` service in the compose file for why sharing one SQLite volume between them
would trade a cosmetic gain for a startup race.

#### Redeploying

```bash
cd /opt/budgetguard-ai
git pull
docker compose -f deploy/docker-compose.shared-proxy.yml up -d --build
```

The database lives on the named volume `budgetguard_budgetguard-data` and
survives rebuilds — uploaded datasets are not lost on redeploy. To start clean,
`docker compose -f deploy/docker-compose.shared-proxy.yml down -v`.

#### Gating the deployment while it is unreleased

To put the instance behind HTTP basic auth — useful while sharing a link before
launch — generate a hash and add it to both site blocks:

```bash
docker exec profin-caddy-1 caddy hash-password --plaintext '<your-password>'
```

```caddyfile
@needs_auth not path /health
basic_auth @needs_auth {
	judge <paste-the-bcrypt-hash-here>
}
```

Only the bcrypt hash goes in the Caddyfile; the plaintext is never stored on
the server. Leave `/health` outside the matcher so uptime checks still work.

This is compatible with Blazor Server. After the browser satisfies the initial
challenge it replays the credentials on the `/_blazor` WebSocket handshake and
the circuit establishes normally.

One trap when verifying: testing with credentials embedded in the URL
(`https://user:pass@host/`) **will appear to fail**, because `fetch()` refuses
to construct a request from a credentialed URL and SignalR's negotiate call
therefore 401s. Authenticate through the browser's own dialog — or navigate to
the credentialed URL once and then to a clean one — before concluding anything
is broken.

#### Two things that will bite you on a shared host

- **Pin the compose project name.** Compose derives it from the directory
  holding the file — `deploy` — which other stacks commonly use too. When two
  projects share a name, each sees the other's containers as orphans and a
  routine `docker compose down --remove-orphans` deletes the wrong application.
  The compose file sets `name: budgetguard` for exactly this reason.
- **The app must honour `X-Forwarded-Proto`.** Behind a TLS-terminating proxy it
  otherwise believes it is on plain HTTP and emits absolute URLs and redirects
  on the wrong scheme. `BudgetGuard.Web` calls `UseForwardedHeaders` and clears
  the known-proxy allow-list, which is safe only because the container publishes
  no host ports.

### Fly.io (alternative)

`fly.toml` is committed and configured.

```bash
# Install flyctl, then:
fly auth login
fly launch --no-deploy --copy-config    # keeps the committed fly.toml
fly deploy
fly open
```

Fly requires a payment method on file even for free-allowance usage, which is
why Render is the recommended path for a quick public link.

For persistence, uncomment the `[mounts]` block in `fly.toml` and run:

```bash
fly volumes create budgetguard_data --size 1 --region fra
```

### Any other container host

The image is a standard ASP.NET Core container that binds `$PORT`. It needs no
external services. Set `ConnectionStrings__BudgetGuard` if you want the database
somewhere other than `/app/data/budgetguard.db`.

---

## Adding a new detection rule

The full checklist is in [CODING_STANDARDS.md](CODING_STANDARDS.md#adding-a-new-detection-rule).
In outline:

1. Add settings to `DetectionSettings` with documented defaults.
2. Write the detector as a pure domain service in
   `src/BudgetGuard.Domain/Detection/<Area>/`. **No package references** — that
   constraint is what keeps the test suite fast and the algorithms verifiable.
3. Return findings carrying an explanation that includes the arithmetic.
4. **Write the tests before wiring it up.** In particular, write the test that
   asserts your detector stays silent on clean data. Every real statistical
   defect in this codebase was found that way.
5. Add a `DetectorKind` member and a weight in `ScoringSettings`.
6. Emit signals from `AnomalyAggregator`.
7. Register in `Application/DependencyInjection.cs`.
8. Add defaults to both `appsettings.json` files.
9. Document the method, thresholds and false-positive risks in
   `docs/DETECTION_METHODOLOGY.md`.

Step 9 is user-facing: that file is rendered as the in-product "How this works"
page, so the published methodology and the UI explanation are one file and
cannot drift.

## Database migrations

```bash
dotnet tool restore

dotnet ef migrations add <Name> \
  --project src/BudgetGuard.Infrastructure \
  --startup-project src/BudgetGuard.Api \
  --output-dir Persistence/Migrations
```

Migrations are applied automatically at startup. That is appropriate here
because the database is a single SQLite file owned by one application, with no
second writer to race. It would not be appropriate against a shared server
database with multiple instances rolling.

## Commit conventions

Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, `test:`), optionally
scoped (`feat(domain):`).

Commit bodies explain **why**, especially for detector changes. Several commits
in this history record a statistical defect, how it was found, and what the
measured effect of the fix was. That reasoning is the most valuable thing in the
log — a future contributor tempted to "simplify" the log-scale comparison or the
Benford noise floor needs to find out from the history why they exist.
