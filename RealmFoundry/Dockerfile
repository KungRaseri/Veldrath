# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and MSBuild props first for layer-cached restore
COPY Directory.Build.props                                     ./
COPY Directory.Packages.props                                  ./
COPY versions/                                                 versions/
COPY RealmFoundry/RealmFoundry.csproj                          RealmFoundry/
COPY RealmFoundry/Directory.Build.props                        RealmFoundry/
COPY RealmUnbound.Contracts/RealmUnbound.Contracts.csproj      RealmUnbound.Contracts/

RUN dotnet restore RealmFoundry/RealmFoundry.csproj

# Copy all source
COPY RealmFoundry/           RealmFoundry/
COPY RealmUnbound.Contracts/ RealmUnbound.Contracts/

WORKDIR /src/RealmFoundry
RUN dotnet publish RealmFoundry.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RealmFoundry.dll"]
