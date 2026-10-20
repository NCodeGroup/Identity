# syntax=docker/dockerfile:1
#
# The production image of NCode.Identity.OpenId.Host (ADR-0178). Build it from the repository root, passing the Node
# major from its single pin (ADR-0152):
#
#   docker buildx build --platform linux/amd64,linux/arm64 \
#     --build-arg NODE_VERSION="$(cat src/frontend/.node-version)" \
#     --file src/NCode.Identity.OpenId.Host/Dockerfile .
#
# Every build stage runs on the build machine's own architecture and cross-publishes for the target, so a multi-arch
# build needs no emulation: the only target-architecture stage copies files and runs nothing.

ARG DOTNET_VERSION=10.0
ARG NODE_VERSION

# The interactive login and consent bundle (apps/auth). Its output is static files, identical on every architecture.
FROM --platform=$BUILDPLATFORM node:${NODE_VERSION}-trixie-slim AS frontend
WORKDIR /source/src/frontend
COPY src/frontend/ ./
# pnpm is installed at the exact version package.json's packageManager field pins.
RUN npm install --global --no-fund --no-audit "$(node --print "require('./package.json').packageManager")"
RUN --mount=type=cache,id=pnpm-store,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile
RUN pnpm exec turbo run build --filter=@ncode/app-auth

# The host and its migration worker, published framework-dependent for the target architecture. The worker ships in
# the host's image so the schema a release migrates to is always the schema that release's host reads (ADR-0180).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
ARG TARGETARCH
# The build context carries no .git directory, so the version comes from version.json alone rather than commit height.
ENV NBGV_GitEngine=Disabled
WORKDIR /source
COPY . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/NCode.Identity.OpenId.Host/NCode.Identity.OpenId.Host.csproj \
        --configuration Release \
        --arch "$TARGETARCH" \
        --no-self-contained \
        --output /app/publish \
        -p:UseAppHost=false \
        -p:GeneratePackageOnBuild=false \
    && dotnet publish src/NCode.Identity.OpenId.Host.MigrationService/NCode.Identity.OpenId.Host.MigrationService.csproj \
        --configuration Release \
        --arch "$TARGETARCH" \
        --no-self-contained \
        --output /app/migrations \
        -p:UseAppHost=false \
        -p:GeneratePackageOnBuild=false

# Visual Studio's container fast mode debugs against this stage (DockerfileFastModeStage defaults to "base"), which
# needs a shell; the production image below is built FROM the chiseled variant instead.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS base
WORKDIR /app
USER $APP_UID
EXPOSE 8080

# The production image: a chiseled (distroless) runtime with no shell or package manager, running as the non-root
# 'app' user by numeric id so Kubernetes can enforce runAsNonRoot. The "extra" variant carries ICU and time zone data.
# The image listens on 8080 (ASPNETCORE_HTTP_PORTS); TLS terminates at the ingress.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app/publish ./
# Run with the working directory /app/migrations and 'dotnet NCode.Identity.OpenId.Host.MigrationService.dll'.
COPY --from=build /app/migrations ./migrations/
COPY --from=frontend /source/src/frontend/apps/auth/dist ./auth/
ENV InteractiveAuthentication__BundleRootPath=/app/auth
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NCode.Identity.OpenId.Host.dll"]
