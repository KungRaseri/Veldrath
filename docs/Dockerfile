# The DocFX documentation site, served by nginx.
#
# Forgejo has no Pages server, so the published docs live in Coolify: this image is built
# there from the repository (public source, so no registry credential is involved) and
# Traefik serves it at docs.veldrath.com. `.github/workflows/docs.yml` queues the deployment
# after a successful main-branch docs build.
#
# docfx is pinned: `templates/veldrath/layout/_master.tmpl` is a copy of the modern template's
# master from this exact version. Bump DOCFX_VERSION here and in the workflow together, and
# re-sync that file (see its header comment).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG DOCFX_VERSION=2.81.0

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

RUN dotnet tool install --global docfx --version "${DOCFX_VERSION}"
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /src
COPY . .

# Both landing pages are real, reviewed files now: index.md (the site front page) and
# api/index.md (the API tree's front page, committed so `/api/` is not a 404). No printf
# fallbacks here anymore — if either is missing, the docfx build says so.
RUN dotnet restore Realm.Full.slnx \
 && docfx metadata docfx.json \
 && docfx build docfx.json \
 && test -f _site/index.html \
 && test -f _site/api/index.html

FROM nginx:alpine
COPY docs/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/_site /usr/share/nginx/html
