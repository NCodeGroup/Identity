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
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c AS base
WORKDIR /app
USER $APP_UID
EXPOSE 8080

# The production image: a chiseled (distroless) runtime with no shell or package manager, running as the non-root
# 'app' user by numeric id so Kubernetes can enforce runAsNonRoot. The "extra" variant carries ICU and time zone data.
# The image listens on 8080 (ASPNETCORE_HTTP_PORTS); TLS terminates at the ingress.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:a9298ccc39c6258ae71228479e2f3f8d2ceb6639a9927262a4b0ff7d319a0e7e AS final
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=frontend /source/frontend/apps/mgmt/dist ./mgmt/
ENV ManagementConsole__BundleRootPath=/app/mgmt
# The console writes one JSON object per line with a UTC timestamp, so a container log collector keeps an entry and its
# exception in one record (ADR-1248).
ENV Logging__Console__FormatterName=json \
    Logging__Console__FormatterOptions__UseUtcTimestamp=true \
    Logging__Console__FormatterOptions__TimestampFormat=O
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NCode.Identity.ManagementConsole.Host.dll"]
