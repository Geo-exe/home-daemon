# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Frontend stage
# ---------------------------------------------------------------------------
# The SPA is built here and later published as the API's static content, so the container
# serves the app and the API from a single origin.
FROM node:24-alpine AS web
WORKDIR /web

# Install from the lockfile first, so this layer is cached until dependencies change.
COPY src/HomeDaemon.Web/package.json src/HomeDaemon.Web/package-lock.json ./
RUN npm ci

COPY src/HomeDaemon.Web/ ./
RUN npm run build

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the build manifests first, so that NuGet restore stays cached until
# a project file actually changes. .editorconfig is required here because the
# src/ projects enforce code style during the build (EnforceCodeStyleInBuild).
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Directory.Build.props src/Directory.Build.props
COPY src/HomeDaemon.Domain/HomeDaemon.Domain.csproj src/HomeDaemon.Domain/
COPY src/HomeDaemon.Application/HomeDaemon.Application.csproj src/HomeDaemon.Application/
COPY src/HomeDaemon.DataAccess/HomeDaemon.DataAccess.csproj src/HomeDaemon.DataAccess/
COPY src/HomeDaemon.Api/HomeDaemon.Api.csproj src/HomeDaemon.Api/
RUN dotnet restore src/HomeDaemon.Api/HomeDaemon.Api.csproj

COPY src/ src/
COPY --from=web /web/dist/ src/HomeDaemon.Api/wwwroot/
RUN dotnet publish src/HomeDaemon.Api/HomeDaemon.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is only used by the HEALTHCHECK below. /data and /backups hold the SQLite
# database and its backups; both are meant to be mounted volumes.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data /backups \
    && chown -R "$APP_UID:$APP_UID" /data /backups

WORKDIR /app
COPY --from=build /app/publish ./

# ASPNETCORE_HTTP_PORTS=8080 is inherited from the base image.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
VOLUME ["/data", "/backups"]

# Run as the non-root "app" user provided by the .NET base image (UID 1654).
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 4 http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "HomeDaemon.Api.dll"]
