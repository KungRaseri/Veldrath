# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and MSBuild props first for layer-cached restore
COPY ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "./"]
COPY versions/                                               versions/
COPY Veldrath.Web/Veldrath.Web.csproj                        Veldrath.Web/
COPY Veldrath.Web/Directory.Build.props                      Veldrath.Web/
COPY Veldrath.Contracts/Veldrath.Contracts.csproj    Veldrath.Contracts/
COPY Veldrath.Auth/Veldrath.Auth.csproj              Veldrath.Auth/
COPY Veldrath.Auth.Blazor/Veldrath.Auth.Blazor.csproj Veldrath.Auth.Blazor/
COPY RealmEngine.Core/RealmEngine.Core.csproj              RealmEngine.Core/
COPY RealmEngine.Core/Directory.Build.props                RealmEngine.Core/
COPY RealmEngine.Shared/RealmEngine.Shared.csproj          RealmEngine.Shared/
COPY Veldrath.GameClient.Core/Veldrath.GameClient.Core.csproj Veldrath.GameClient.Core/
COPY Veldrath.GameClient.Components/Veldrath.GameClient.Components.csproj Veldrath.GameClient.Components/
COPY RealmEngine.Data/RealmEngine.Data.csproj               RealmEngine.Data/
COPY RealmEngine.Data/Directory.Build.props                RealmEngine.Data/

RUN dotnet restore Veldrath.Web/Veldrath.Web.csproj

# Copy all source
COPY Veldrath.Web/           Veldrath.Web/
COPY Veldrath.Contracts/ Veldrath.Contracts/
COPY Veldrath.Auth/          Veldrath.Auth/
COPY Veldrath.Auth.Blazor/   Veldrath.Auth.Blazor/
COPY RealmEngine.Core/           RealmEngine.Core/
COPY RealmEngine.Shared/         RealmEngine.Shared/
COPY RealmEngine.Data/          RealmEngine.Data/
COPY Veldrath.GameClient.Core/          Veldrath.GameClient.Core/
COPY Veldrath.GameClient.Components/    Veldrath.GameClient.Components/

WORKDIR /src/Veldrath.Web
RUN dotnet publish Veldrath.Web.csproj -c Release -o /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8082
ENV ASPNETCORE_URLS=http://+:8082

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Veldrath.Web.dll"]
