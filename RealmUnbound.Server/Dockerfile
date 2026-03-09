# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restore
COPY RealmUnbound.Server/RealmUnbound.Server.csproj        RealmUnbound.Server/
COPY RealmEngine.Core/RealmEngine.Core.csproj              RealmEngine.Core/
COPY RealmEngine.Data/RealmEngine.Data.csproj              RealmEngine.Data/
COPY RealmEngine.Shared/RealmEngine.Shared.csproj          RealmEngine.Shared/
COPY Directory.Build.props                                 ./

RUN dotnet restore RealmUnbound.Server/RealmUnbound.Server.csproj

# Copy all source
COPY RealmUnbound.Server/   RealmUnbound.Server/
COPY RealmEngine.Core/      RealmEngine.Core/
COPY RealmEngine.Data/      RealmEngine.Data/
COPY RealmEngine.Shared/    RealmEngine.Shared/

WORKDIR /src/RealmUnbound.Server
RUN dotnet publish RealmUnbound.Server.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Copy JSON game catalog data (RealmEngine.Data does not embed these in build output)
COPY RealmEngine.Data/Data/Json ./Data/Json

EXPOSE 8080
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RealmUnbound.Server.dll"]
