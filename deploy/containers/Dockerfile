# syntax=docker/dockerfile:1

# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first so NuGet restore is cached separately
COPY AgentFederation.sln Directory.Build.props global.json ./
COPY src/Federation.Protocol/Federation.Protocol.csproj             src/Federation.Protocol/
COPY src/Federation.Deliberation/Federation.Deliberation.csproj     src/Federation.Deliberation/
COPY src/Federation.Cryptography/Federation.Cryptography.csproj     src/Federation.Cryptography/
COPY src/Federation.Identity/Federation.Identity.csproj             src/Federation.Identity/
COPY src/Federation.Storage/Federation.Storage.csproj               src/Federation.Storage/
COPY src/Federation.Sync/Federation.Sync.csproj                     src/Federation.Sync/
COPY src/Federation.Infrastructure/Federation.Infrastructure.csproj src/Federation.Infrastructure/
COPY src/Federation.Storage.AzureTables/Federation.Storage.AzureTables.csproj src/Federation.Storage.AzureTables/
COPY src/Federation.Relay/Federation.Relay.csproj                   src/Federation.Relay/

RUN dotnet restore src/Federation.Relay/Federation.Relay.csproj

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/Federation.Relay/Federation.Relay.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Create non-root user
RUN addgroup --system federation && adduser --system --ingroup federation federation
WORKDIR /app

COPY --from=build /app/publish .

USER federation

ENV ASPNETCORE_URLS=http://+:5000
ENV StorageMode=Sqlite
ENV DatabasePath=/data/federation-relay.db

EXPOSE 5000

ENTRYPOINT ["dotnet", "federation-relay.dll"]
