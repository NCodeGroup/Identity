# syntax=docker/dockerfile:1
#
# The production image of NCode.Identity.ManagementConsole.Host (ADR-1458). Build it from the repository root; it takes
# no build argument:
#
#   docker buildx build --platform linux/amd64,linux/arm64 \
#     --file hosts/NCode.Identity.ManagementConsole.Host/Dockerfile .
#
# Every base image is pinned by tag and by the digest of its multi-architecture index, and Dependabot moves the digests
# (ADR-1249). The Node major equals the single pin in frontend/.node-version, which the `Console image` workflow
# checks (ADR-1152).
#
# Every build stage runs on the build machine's own architecture and cross-publishes for the target, so a multi-arch
# build needs no emulation: the only target-architecture stage copies files and runs nothing.

# The management console bundle (apps/mgmt). Its output is static files, identical on every architecture.
FROM --platform=$BUILDPLATFORM node:26-trixie-slim@sha256:5adad64f9a1fca2d51972caa65983634c8a774eb300d72b20f7f2a0f60368072 AS frontend
WORKDIR /source/frontend
COPY frontend/ ./
# pnpm is installed at the exact version package.json's packageManager field pins.
RUN npm install --global --no-fund --no-audit "$(node --print "require('./package.json').packageManager")"
RUN --mount=type=cache,id=pnpm-store,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile
RUN pnpm exec turbo run build --filter=@ncode/app-mgmt

# The host, published framework-dependent for the target architecture.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
ARG TARGETARCH
# The build context carries no .git directory, so the version comes from version.json alone rather than commit height.
ENV NBGV_GitEngine=Disabled
WORKDIR /source
COPY . .
# A multi-arch build runs this stage once per target at the same time. The NuGet cache is locked, not shared, because
# NuGet extracts a package through a temporary file that a concurrent restore of the same package cannot see.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish hosts/NCode.Identity.ManagementConsole.Host/NCode.Identity.ManagementConsole.Host.csproj \
        --configuration Release \
        --arch "$TARGETARCH" \
        --no-self-contained \
        --output /app/publish \
        -p:UseAppHost=false \
        -p:GeneratePackageOnBuild=false

# Visual Studio's container fast mode debugs against this stage (DockerfileFastModeStage defaults to "base"), which
# needs a shell; the production image below is built FROM the chiseled variant instead.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS base
WORKDIR /app
USER $APP_UID
EXPOSE 8080

# The production image: a chiseled (distroless) runtime with no shell or package manager, running as the non-root
# 'app' user by numeric id so Kubernetes can enforce runAsNonRoot. The "extra" variant carries ICU and time zone data.
# The image serves HTTPS only on 8443 (ASPNETCORE_URLS takes precedence over the base image's ASPNETCORE_HTTP_PORTS)
# from the PEM certificate and key mounted at /etc/ncode-identity/tls; the host refuses to start in Production without a
# usable certificate (ADR-1503).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:cae5b962bfb786606def03d250c6a645cc98839095c89e806e4bd8978a68115b AS final
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=frontend /source/frontend/apps/mgmt/dist ./mgmt/
ENV ManagementConsole__BundleRootPath=/app/mgmt
# The console writes one JSON object per line with a UTC timestamp, so a container log collector keeps an entry and its
# exception in one record (ADR-1248).
ENV Logging__Console__FormatterName=json \
    Logging__Console__FormatterOptions__UseUtcTimestamp=true \
    Logging__Console__FormatterOptions__TimestampFormat=O
ENV ASPNETCORE_URLS=https://+:8443 \
    Kestrel__Certificates__Default__Path=/etc/ncode-identity/tls/tls.crt \
    Kestrel__Certificates__Default__KeyPath=/etc/ncode-identity/tls/tls.key
USER $APP_UID
EXPOSE 8443
ENTRYPOINT ["dotnet", "NCode.Identity.ManagementConsole.Host.dll"]
