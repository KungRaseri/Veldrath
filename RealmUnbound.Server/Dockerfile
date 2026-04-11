# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and MSBuild props first for layer-cached restore
COPY Directory.Build.props                                 ./
COPY Directory.Packages.props                              ./
COPY versions/                                             versions/
COPY Veldrath.Server/Veldrath.Server.csproj        Veldrath.Server/
COPY Veldrath.Server/Directory.Build.props             Veldrath.Server/
COPY Veldrath.Contracts/Veldrath.Contracts.csproj  Veldrath.Contracts/
COPY RealmEngine.Core/RealmEngine.Core.csproj              RealmEngine.Core/
COPY RealmEngine.Core/Directory.Build.props                RealmEngine.Core/
COPY RealmEngine.Data/RealmEngine.Data.csproj              RealmEngine.Data/
COPY RealmEngine.Data/Directory.Build.props                RealmEngine.Data/
COPY RealmEngine.Shared/RealmEngine.Shared.csproj          RealmEngine.Shared/
COPY RealmEngine.Shared/Directory.Build.props              RealmEngine.Shared/
COPY Veldrath.Assets/Veldrath.Assets.csproj        Veldrath.Assets/
COPY Veldrath.Assets/Directory.Build.props             Veldrath.Assets/

RUN dotnet restore Veldrath.Server/Veldrath.Server.csproj

# Copy all source
COPY Veldrath.Server/     Veldrath.Server/
COPY Veldrath.Contracts/  Veldrath.Contracts/
COPY RealmEngine.Core/        RealmEngine.Core/
COPY RealmEngine.Data/        RealmEngine.Data/
COPY RealmEngine.Shared/      RealmEngine.Shared/
COPY Veldrath.Assets/     Veldrath.Assets/

WORKDIR /src/Veldrath.Server
RUN dotnet publish Veldrath.Server.csproj -c Release -o /app/publish --no-restore

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

ENTRYPOINT ["dotnet", "Veldrath.Server.dll"]
