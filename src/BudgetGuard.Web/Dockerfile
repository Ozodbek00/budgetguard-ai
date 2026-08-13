# Multi-stage build for the BudgetGuard AI Blazor Server front end.
# Build context is the repository root, so that Directory.Build.props,
# Directory.Packages.props and the referenced projects are all in scope:
#   docker build -f src/BudgetGuard.Web/Dockerfile -t budgetguard-web .

# ---------- restore ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy only what NuGet restore needs first. This layer is cached until a
# project file or package version actually changes, so ordinary source edits
# skip the slowest step of the build entirely.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BudgetGuard.Domain/BudgetGuard.Domain.csproj              src/BudgetGuard.Domain/
COPY src/BudgetGuard.Application/BudgetGuard.Application.csproj    src/BudgetGuard.Application/
COPY src/BudgetGuard.Infrastructure/BudgetGuard.Infrastructure.csproj src/BudgetGuard.Infrastructure/
COPY src/BudgetGuard.Web/BudgetGuard.Web.csproj                    src/BudgetGuard.Web/

RUN dotnet restore src/BudgetGuard.Web/BudgetGuard.Web.csproj

# ---------- publish ----------
FROM restore AS publish
COPY src/ src/

RUN dotnet publish src/BudgetGuard.Web/BudgetGuard.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

# The SQLite file lives here. Declared as a volume so a platform that offers
# persistent storage can mount one; without a mount the database is ephemeral
# and resets on redeploy, which is acceptable because the demo dataset
# regenerates on demand. See docs/adr/0002-sqlite-over-postgres-for-mvp.md.
RUN mkdir -p /app/data && chown -R app:app /app
VOLUME /app/data

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__BudgetGuard="Data Source=/app/data/budgetguard.db"

# Run unprivileged. The aspnet image ships a non-root `app` user for this.
USER app

EXPOSE 8080

# Hosting platforms (Render, Fly, Cloud Run) inject the port to bind as $PORT.
# Falling back to 8080 keeps `docker run` working locally with no environment.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet BudgetGuard.Web.dll --urls http://0.0.0.0:${PORT:-8080}"]
