// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;
using NCode.Identity.OpenId.LocalDev.Frontend;

// ASPIRE ORCHESTRATION-ONLY HOST (ADR-1071): a flat resource graph that is the single one-stop entry to build, run,
// and debug the whole local stack (the OpenID host + the frontend apps) with deterministic startup ordering.
// This is a SIBLING of the host, not nested inside it — the host stays a single reference OP process.
// Keep this file declarative (Add*/With*/WaitFor); no reusable logic belongs here.

// Aspire spawns pnpm/node for the frontend resources and discovers them on PATH. When Node is NVS-managed and not
// globally linked, the AppHost (launched by VS Code) does not inherit it, so the frontend resources fail to start.
var builder = DistributedApplication.CreateBuilder(args);

// The frontend's Node major is pinned once, in frontend/.node-version, which CI and NVS read too. The guard puts
// the newest NVS-managed install of that major on this process's PATH (the child resources inherit it), then fails the
// AppHost at startup, naming ./build/frontend-install.ps1, when no Node of the pinned major is available or
// node_modules does not match the pnpm pin and lockfile (ADR-1071).
new FrontendToolchainGuard(
    Path.Combine(builder.AppHostDirectory, "..", "..", "frontend")
).EnsureReady();

// Every OP instance started below shares the one dev database, so they must all unwrap the same master keys: they are
// handed the same development root key, as a key file (ADR-1235). The key is generated once per machine, outside the
// repository, so no root key is ever committed.
var developmentSecretsDirectory = DevelopmentSecretFile.GetDefaultDirectory();
var rootKeyPath = DevelopmentSecretFile.Ensure(developmentSecretsDirectory, "root-key");

// No development credential is committed (ADR-1370): the bootstrap administrator's client secret and the developer
// administrator's password are generated once per machine beside the root key. The bootstrap secret is configured
// Base64Url-encoded, so the file's text is the secret a client presents.
var bootstrapAdminSecretPath = DevelopmentSecretFile.Ensure(
    developmentSecretsDirectory,
    "bootstrap-admin-secret"
);
var bootstrapAdminClientSecret = DevelopmentSecretFile.ReadBase64UrlEncoded(
    bootstrapAdminSecretPath
);
var adminPasswordPath = DevelopmentSecretFile.Ensure(developmentSecretsDirectory, "admin-password");
var adminPassword = DevelopmentSecretFile.ReadText(adminPasswordPath);

// The management console's BFF host (`console`) is a confidential client of the `api`, so its two private keys — the
// private_key_jwt client key and the DPoP key — are generated once per machine as P-256 PEM files beside the root key,
// and the console is handed their paths. The OP's outbound guard never fetches a loopback jwks_uri, so the `api`
// registers the client key's public JWKS by value instead.
var consoleClientKeyPath = DevelopmentKeyFile.EnsureEcPrivateKey(
    developmentSecretsDirectory,
    "mgmt-client.pem"
);
var consoleDPoPKeyPath = DevelopmentKeyFile.EnsureEcPrivateKey(
    developmentSecretsDirectory,
    "mgmt-dpop.pem"
);
var consoleClientJwks = DevelopmentKeyFile.ReadPublicJwks(consoleClientKeyPath);

// The conformance seed's client secrets and test user password are generated per machine too, beside the root key,
// where the conformance runner (build/conformance/run-conformance.ps1) reads the same files.
var conformanceSeedEnvironment = ConformanceSeedSecrets.GetEnvironment(developmentSecretsDirectory);

// The dev database is Postgres in a container (ADR-1073). WithDataVolume gives it a STABLE, named volume so data
// survives restarts (persistent by default); the matching build/dev-db-reset.ps1 removes exactly this volume for an
// on-demand purge. Aspire auto-detects the container runtime (Docker or Podman), so the graph is portable across dev
// machines. `openiddb` is the single database holding the OpenID data, the seeded signing key, the developer
// DataProtection key ring, and the tenants' master keys — one purge resets all of them together. The master keys are
// wrapped by the per-machine development root key above, which lives outside the database, so the reset keeps it and
// a fresh database simply gets new master keys under it.
var postgres = builder.AddPostgres("postgres").WithDataVolume("ncode-identity-openiddb-data");
var openIdDb = postgres.AddDatabase("openiddb");

// The shared state every OP replica must see — DPoP nonces and the single-use records of DPoP proofs and client
// assertions (ADR-1164) — lives in Valkey, as in a multi-replica deployment, so local development exercises the same
// adapter. It is a cache, not a system of record, so it has no data volume: a restart only clears records that expire
// within minutes. WithReference injects the 'valkey' connection string (with the generated password).
var valkey = builder.AddValkey("valkey");

// Dedicated migration resource (issue #144): applies the Npgsql migrations for both contexts, then exits. Dependents
// WaitForCompletion on it, so a migration failure surfaces as a failed dashboard resource that blocks the api — it
// never manifests as an api crash. WaitFor(postgres) holds it until the database is accepting connections.
var migrations = builder
    .AddProject<Projects.NCode_Identity_OpenId_Host_MigrationService>("migrations")
    .WithReference(openIdDb)
    .WaitFor(postgres);

// The reference OpenID Provider host. Its default launch profile supplies the Development environment variables and
// the fixed dev endpoints (https://localhost:5001, http://localhost:5000) the rest of the stack is pinned to — the
// tenant's issuer (pinned by the same profile), the browser-published bootstrap document, the seeded SPA redirect URIs,
// and the developer CORS origins all assume those ports. WithHttpHealthCheck gates dependents on the app being HEALTHY
// (its /health/ready returns 200 once Kestrel is listening and the database answers), not merely "process started", which is what deterministically
// removes the UI-before-API race.
// WithReference injects the 'openiddb' connection string; WaitForCompletion holds the api until migrations succeed.
// The SecretProtection__ variables select the `local` root key provider and point it at the development root key file.
// The BootstrapAdmin__ and DeveloperInteractiveSeed__ variables hand it the per-machine development credentials.
var api = builder
    .AddProject<Projects.NCode_Identity_OpenId_Host>("api")
    .WithHttpHealthCheck("/health/ready")
    .WithReference(openIdDb)
    .WithReference(valkey)
    .WithEnvironment("SecretProtection__RootKeyProvider", "local")
    .WithEnvironment("SecretProtection__RootKeyProviders__Local__KeyPath", rootKeyPath)
    .WithEnvironment("BootstrapAdmin__ClientSecret", bootstrapAdminClientSecret)
    .WithEnvironment("DeveloperInteractiveSeed__AdminPassword", adminPassword)
    .WithEnvironment("DeveloperInteractiveSeed__BffClients__0__Jwks", consoleClientJwks)
    .WaitFor(valkey)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints();

// A SECOND, conformance-only OP instance: the same host project launched under the Conformance environment
// (the ConformanceAspire launch profile, ASPNETCORE_ENVIRONMENT=Conformance) on its own ports
// (https://localhost:5003, http://localhost:5002), sharing the one Postgres. Its appsettings.Conformance.json overlay
// defines its own OpenId:Platform:Settings root ceiling that advertises the available-but-off-by-default capabilities
// (Session Management, Front-Channel Logout, the client_credentials grant — ADR-1088) so the conformance dashboard
// reports them, while the `api` above stays the locked-down secure default. The same overlay sets Cors:AllowedOrigins
// (the Development CORS default does not apply outside Development) so the 5175 dashboard can read its discovery
// cross-origin, and serves its own `conformance` tenant: every tenant is published under its own stored issuer
// (ADR-1165), so the two instances cannot share the `default` tenant in the shared database — the launch profile pins
// this one's issuer to https://localhost:5003. WaitForCompletion(migrations) holds it until the shared schema is
// applied. It gets the same development root key as the `api`, because both unwrap the master keys in the one database,
// and the per-machine credentials of the conformance seed, which the overlay lists without them.
var conformanceApi = builder
    .AddProject<Projects.NCode_Identity_OpenId_Host>(
        "conformanceapi",
        launchProfileName: "ConformanceAspire"
    )
    .WithHttpHealthCheck("/health/ready")
    .WithReference(openIdDb)
    .WithReference(valkey)
    .WithEnvironment("SecretProtection__RootKeyProvider", "local")
    .WithEnvironment("SecretProtection__RootKeyProviders__Local__KeyPath", rootKeyPath)
    .WithEnvironment("BootstrapAdmin__ClientSecret", bootstrapAdminClientSecret)
    .WithConformanceSeedSecrets(conformanceSeedEnvironment)
    .WaitFor(valkey)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints();

// The interactive login/consent surface (apps/auth) is a BACKEND-SERVED bundle: its `dev` script is `vite build
// --watch`, which continuously rebuilds `dist` for the host to serve under /account/* (ADR-1062). It is a
// build-watcher with no HTTP endpoint of its own, so it is modeled as a plain JavaScript app resource, not a Vite dev
// server. The host serves whatever `dist` exists; on a cold start give the first build a moment to finish.
// `install: false` — the pnpm workspace is installed ONCE out-of-band by build/frontend-install.ps1; letting each app
// auto-install would run two `pnpm install` processes concurrently against the shared, symlinked node_modules and
// collide (ERR_PNPM_EBUSY).
builder.AddJavaScriptApp("auth", "../../frontend/apps/auth").WithPnpm(install: false);

// The management console bundle (apps/mgmt) is a BFF-SERVED bundle, like `auth`: its `dev` script is `vite build
// --watch`, which continuously rebuilds `dist` for the console host below to serve. It is a build-watcher with no HTTP
// endpoint of its own, so it is a plain JavaScript app resource, not a Vite dev server. The console host serves
// whatever `dist` exists; on a cold start give the first build a moment to finish.
builder.AddJavaScriptApp("mgmt", "../../frontend/apps/mgmt").WithPnpm(install: false);

// The management console's BFF host (`console`) is a confidential client of the `api` on https://localhost:5005 (the
// origin the developer seed's `mgmt-console` redirect URIs assume). It serves the `mgmt` bundle, its own /bff/*
// endpoints and the forwarded management API on one origin, and holds the browser's tokens. Its private_key_jwt and
// DPoP keys are the per-machine PEM files generated above, handed over by path as the `mgmt-client` and `mgmt-dpop`
// key rings. WaitFor holds startup until the `api` is healthy so the first sign-in cannot race it.
builder
    .AddProject<Projects.NCode_Identity_ManagementConsole_Host>("console")
    .WithHttpHealthCheck("/health/ready")
    .WithEnvironment("Bff__KeyRings__mgmt-client__PemFiles__0", consoleClientKeyPath)
    .WithEnvironment("Bff__KeyRings__mgmt-dpop__PemFiles__0", consoleDPoPKeyPath)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

// The Playground flow explorer (apps/playground) is a STANDALONE Vite dev server pinned to https://localhost:5174 (the
// port the seeded `playground-spa` redirect URI and AddDeveloperCors allowlist assume). It drives a live
// authorization-code + PKCE round trip against the OP, so it has its own seeded public client and CORS origin, and
// proxies /config/bootstrap.json to the api, discovering its `playground` surface coordinates at boot (ADR-1064). Like
// every standalone surface below, it serves HTTPS with the ASP.NET Core development certificate the OP hosts use
// (WithHttpsDeveloperCertificate): Aspire runs Vite under a wrapper config that sets `server.https` from that
// certificate and switches the endpoint's scheme to https, so the browser sees one trusted certificate across the
// stack. WithReference injects the service-discovery variable the proxy targets, WaitFor holds startup until the API is
// healthy, and the un-proxied fixed port lets the OAuth redirect callback resolve.
builder
    .AddViteApp("playground", "../../frontend/apps/playground")
    .WithPnpm(install: false)
    .WithHttpsDeveloperCertificate()
    .WithEndpoint(
        "http",
        endpoint =>
        {
            endpoint.Port = 5174;
            endpoint.TargetPort = 5174;
            endpoint.IsProxied = false;
        },
        createIfNotExists: false
    )
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

// The conformance posture dashboard (apps/conformance) is a STANDALONE, read-only Vite dev server pinned to
// https://localhost:5175, served with the same development certificate as the Playground. It only audits the OP's
// published discovery metadata, so it needs no OAuth client; it proxies /config/bootstrap.json to the dedicated
// conformance OP (`conformanceapi`, which advertises the opt-in capabilities) to learn its issuer, then reads that
// issuer's discovery cross-origin (allowlisted by the Conformance overlay's Cors:AllowedOrigins). WaitFor holds startup
// until that OP is healthy so the first discovery read cannot race it.
builder
    .AddViteApp("conformance", "../../frontend/apps/conformance")
    .WithPnpm(install: false)
    .WithHttpsDeveloperCertificate()
    .WithEndpoint(
        "http",
        endpoint =>
        {
            endpoint.Port = 5175;
            endpoint.TargetPort = 5175;
            endpoint.IsProxied = false;
        },
        createIfNotExists: false
    )
    .WithReference(conformanceApi)
    .WaitFor(conformanceApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
