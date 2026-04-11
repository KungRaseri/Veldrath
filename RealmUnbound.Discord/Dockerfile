# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and MSBuild props first for layer-cached restore
COPY Directory.Build.props                                      ./
COPY Directory.Packages.props                                   ./
COPY versions/                                                  versions/
COPY Veldrath.Discord/Veldrath.Discord.csproj           Veldrath.Discord/
COPY Veldrath.Discord/Directory.Build.props                 Veldrath.Discord/
COPY Veldrath.Contracts/Veldrath.Contracts.csproj       Veldrath.Contracts/
COPY RealmEngine.Core/RealmEngine.Core.csproj                   RealmEngine.Core/
COPY RealmEngine.Core/Directory.Build.props                     RealmEngine.Core/
COPY RealmEngine.Data/RealmEngine.Data.csproj                   RealmEngine.Data/
COPY RealmEngine.Data/Directory.Build.props                     RealmEngine.Data/
COPY RealmEngine.Shared/RealmEngine.Shared.csproj               RealmEngine.Shared/
COPY RealmEngine.Shared/Directory.Build.props                   RealmEngine.Shared/

RUN dotnet restore Veldrath.Discord/Veldrath.Discord.csproj

# Copy all source
COPY Veldrath.Discord/    Veldrath.Discord/
COPY Veldrath.Contracts/  Veldrath.Contracts/
COPY RealmEngine.Core/        RealmEngine.Core/
COPY RealmEngine.Data/        RealmEngine.Data/
COPY RealmEngine.Shared/      RealmEngine.Shared/

WORKDIR /src/Veldrath.Discord
RUN dotnet publish Veldrath.Discord.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Bot token must be injected at runtime — never bake secrets into the image.
# Set Discord__Token via docker-compose environment or a Docker secret.
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Veldrath.Discord.dll"]
