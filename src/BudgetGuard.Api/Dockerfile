# Multi-stage build for the BudgetGuard AI Web API (Swagger surface).
# Build context is the repository root:
#   docker build -f src/BudgetGuard.Api/Dockerfile -t budgetguard-api .

# ---------- restore ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BudgetGuard.Domain/BudgetGuard.Domain.csproj              src/BudgetGuard.Domain/
COPY src/BudgetGuard.Application/BudgetGuard.Application.csproj    src/BudgetGuard.Application/
COPY src/BudgetGuard.Infrastructure/BudgetGuard.Infrastructure.csproj src/BudgetGuard.Infrastructure/
COPY src/BudgetGuard.Api/BudgetGuard.Api.csproj                    src/BudgetGuard.Api/

RUN dotnet restore src/BudgetGuard.Api/BudgetGuard.Api.csproj

# ---------- publish ----------
FROM restore AS publish
COPY src/ src/

RUN dotnet publish src/BudgetGuard.Api/BudgetGuard.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

RUN mkdir -p /app/data && chown -R app:app /app
VOLUME /app/data

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__BudgetGuard="Data Source=/app/data/budgetguard.db"

USER app

EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "exec dotnet BudgetGuard.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
