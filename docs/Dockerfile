# The DocFX documentation site, served by nginx.
#
# Forgejo has no Pages server, so the published docs live in Coolify: this image is built
# there from the repository (public source, so no registry credential is involved) and
# Traefik serves it at docs.veldrath.com. `.github/workflows/docs.yml` queues the deployment
# after a successful main-branch docs build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

RUN dotnet tool install --global docfx
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /src
COPY . .

# Mirrors the steps in .github/workflows/docs.yml: API metadata from the solution, a landing
# page for the generated API tree, then the site build (docfx.json writes to _site/).
RUN dotnet restore Realm.Full.slnx \
 && docfx metadata docfx.json \
 && printf '# API Reference\n\nBrowse the RealmEngine API documentation using the navigation on the left.\n' > api/index.md \
 && docfx build docfx.json \
 && test -d _site

FROM nginx:alpine
COPY --from=build /src/_site /usr/share/nginx/html
