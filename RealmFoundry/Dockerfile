# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and MSBuild props first for layer-cached restore
COPY Directory.Build.props                                     ./
COPY Directory.Packages.props                                  ./
COPY versions/                                                 versions/
COPY RealmFoundry/RealmFoundry.csproj                          RealmFoundry/
COPY RealmFoundry/Directory.Build.props                        RealmFoundry/
COPY Veldrath.Contracts/Veldrath.Contracts.csproj      Veldrath.Contracts/
COPY Veldrath.Auth/Veldrath.Auth.csproj                Veldrath.Auth/
COPY Veldrath.Auth.Blazor/Veldrath.Auth.Blazor.csproj  Veldrath.Auth.Blazor/
COPY Veldrath.GameClient.Components/Veldrath.GameClient.Components.csproj Veldrath.GameClient.Components/
COPY Veldrath.GameClient.Core/Veldrath.GameClient.Core.csproj Veldrath.GameClient.Core/
COPY RealmEngine.Shared/RealmEngine.Shared.csproj      RealmEngine.Shared/

RUN dotnet restore RealmFoundry/RealmFoundry.csproj

# Copy all source
COPY RealmFoundry/           RealmFoundry/
COPY Veldrath.Contracts/ Veldrath.Contracts/
COPY Veldrath.Auth/          Veldrath.Auth/
COPY Veldrath.Auth.Blazor/   Veldrath.Auth.Blazor/
COPY Veldrath.GameClient.Components/    Veldrath.GameClient.Components/
COPY Veldrath.GameClient.Core/          Veldrath.GameClient.Core/
COPY RealmEngine.Shared/                RealmEngine.Shared/

WORKDIR /src/RealmFoundry
RUN dotnet publish RealmFoundry.csproj -c Release -o /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8081

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RealmFoundry.dll"]
