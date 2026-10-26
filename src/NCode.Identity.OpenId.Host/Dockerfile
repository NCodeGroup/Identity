# syntax=docker/dockerfile:1
#
# The production image of NCode.Identity.OpenId.Host (ADR-0178). Build it from the repository root; it takes no build
# argument:
#
#   docker buildx build --platform linux/amd64,linux/arm64 \
#     --file src/NCode.Identity.OpenId.Host/Dockerfile .
#
# Every base image is pinned by tag and by the digest of its multi-architecture index, and Dependabot moves the digests
# (ADR-0249). The Node major equals the single pin in src/frontend/.node-version, which the `Host image` workflow
# checks (ADR-0152).
#
# Every build stage runs on the build machine's own architecture and cross-publishes for the target, so a multi-arch
# build needs no emulation: the only target-architecture stage copies files and runs nothing.

# The interactive login and consent bundle (apps/auth). Its output is static files, identical on every architecture.
FROM --platform=$BUILDPLATFORM node:26-trixie-slim@sha256:36793bcbb160a6a16cede1e62b93f84e26b78a972a8a4c0b879c1c12f22761ec AS frontend
WORKDIR /source/src/frontend
COPY src/frontend/ ./
# pnpm is installed at the exact version package.json's packageManager field pins.
RUN npm install --global --no-fund --no-audit "$(node --print "require('./package.json').packageManager")"
RUN --mount=type=cache,id=pnpm-store,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile
RUN pnpm exec turbo run build --filter=@ncode/app-auth

# The host and its migration worker, published framework-dependent for the target architecture. The worker ships in
# the host's image so the schema a release migrates to is always the schema that release's host reads (ADR-0180).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ARG TARGETARCH
# The build context carries no .git directory, so the version comes from version.json alone rather than commit height.
ENV NBGV_GitEngine=Disabled
WORKDIR /source
COPY . .
# A multi-arch build runs this stage once per target at the same time. The NuGet cache is locked, not shared, because
# NuGet extracts a package through a temporary file that a concurrent restore of the same package cannot see.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS base
WORKDIR /app
USER $APP_UID
EXPOSE 8080

# The production image: a chiseled (distroless) runtime with no shell or package manager, running as the non-root
# 'app' user by numeric id so Kubernetes can enforce runAsNonRoot. The "extra" variant carries ICU and time zone data.
# The image listens on 8080 (ASPNETCORE_HTTP_PORTS); TLS terminates at the ingress.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:9b755ea799ae649141a073e30d8b62d8095a5465af2b7694d17866e8c05131d8 AS final
WORKDIR /app
COPY --from=build /app/publish ./
# Run with the working directory /app/migrations and 'dotnet NCode.Identity.OpenId.Host.MigrationService.dll'.
COPY --from=build /app/migrations ./migrations/
COPY --from=frontend /source/src/frontend/apps/auth/dist ./auth/
ENV InteractiveAuthentication__BundleRootPath=/app/auth
# The console writes one JSON object per line with a UTC timestamp, so a container log collector keeps an entry and its
# exception in one record (ADR-0248).
ENV Logging__Console__FormatterName=json \
    Logging__Console__FormatterOptions__UseUtcTimestamp=true \
    Logging__Console__FormatterOptions__TimestampFormat=O
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NCode.Identity.OpenId.Host.dll"]
