# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and MSBuild props first for layer-cached restore
COPY Directory.Build.props                                 ./
COPY Directory.Packages.props                              ./
COPY versions/                                             versions/
COPY RealmUnbound.Server/RealmUnbound.Server.csproj        RealmUnbound.Server/
COPY RealmUnbound.Server/Directory.Build.props             RealmUnbound.Server/
COPY RealmUnbound.Contracts/RealmUnbound.Contracts.csproj  RealmUnbound.Contracts/
COPY RealmEngine.Core/RealmEngine.Core.csproj              RealmEngine.Core/
COPY RealmEngine.Core/Directory.Build.props                RealmEngine.Core/
COPY RealmEngine.Data/RealmEngine.Data.csproj              RealmEngine.Data/
COPY RealmEngine.Data/Directory.Build.props                RealmEngine.Data/
COPY RealmEngine.Shared/RealmEngine.Shared.csproj          RealmEngine.Shared/
COPY RealmEngine.Shared/Directory.Build.props              RealmEngine.Shared/

RUN dotnet restore RealmUnbound.Server/RealmUnbound.Server.csproj

# Copy all source
COPY RealmUnbound.Server/     RealmUnbound.Server/
COPY RealmUnbound.Contracts/  RealmUnbound.Contracts/
COPY RealmEngine.Core/        RealmEngine.Core/
COPY RealmEngine.Data/      RealmEngine.Data/
COPY RealmEngine.Shared/    RealmEngine.Shared/

WORKDIR /src/RealmUnbound.Server
RUN dotnet publish RealmUnbound.Server.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RealmUnbound.Server.dll"]
