#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

// ASPIRE ORCHESTRATION-ONLY HOST (ADR-0071): a flat resource graph that is the single one-stop entry to build, run,
// and debug the whole local stack (the Playground OP host + the frontend apps) with deterministic startup ordering.
// This is a SIBLING of the Playground, not nested inside it — the Playground stays a single reference OP process.
// Keep this file declarative (Add*/With*/WaitFor); no reusable logic belongs here.

// Aspire spawns pnpm/node for the frontend resources and discovers them on PATH. When Node is NVS-managed and not
// globally linked, the AppHost (launched by VS Code) does not inherit it, so the mgmt/auth resources fail to start.
// Prepend the newest NVS-managed Node 22 to this process's PATH; the child resources inherit it. No-op if Node is
// already resolvable or NVS is absent (run `nvs add 22` + `build/frontend-install.ps1` once for a fresh clone).
EnsureNvsNodeOnPath();

var builder = DistributedApplication.CreateBuilder(args);

// The dev database is Postgres in a container (ADR-0073). WithDataVolume gives it a STABLE, named volume so data
// survives restarts (persistent by default); the matching build/dev-db-reset.ps1 removes exactly this volume for an
// on-demand purge. Aspire auto-detects the container runtime (Docker or Podman), so the graph is portable across dev
// machines. `openiddb` is the single database holding the OpenID data, the seeded signing key, and the developer
// DataProtection key ring — one purge resets all three together.
var postgres = builder.AddPostgres("postgres").WithDataVolume("ncode-identity-openiddb-data");
var openIdDb = postgres.AddDatabase("openiddb");

// Dedicated migration resource (U12): applies the Npgsql migrations for both contexts, then exits. Dependents
// WaitForCompletion on it, so a migration failure surfaces as a failed dashboard resource that blocks the api — it
// never manifests as an api crash. WaitFor(postgres) holds it until the database is accepting connections.
var migrations = builder
    .AddProject<Projects.NCode_Identity_OpenId_Playground_MigrationService>("migrations")
    .WithReference(openIdDb)
    .WaitFor(postgres);

// The reference OpenID Provider host. Its default launch profile supplies the Development environment variables and
// the fixed dev endpoints (https://localhost:5001, http://localhost:5000) the rest of the stack is pinned to — the
// issuer, the browser-published bootstrap document, the seeded SPA redirect URIs, and the developer CORS origins all
// assume those ports. WithHttpHealthCheck gates dependents on the app being HEALTHY (its /health returns 200 once
// Kestrel is listening), not merely "process started", which is what deterministically removes the UI-before-API race.
// WithReference injects the 'openiddb' connection string; WaitForCompletion holds the api until migrations succeed.
var api = builder
    .AddProject<Projects.NCode_Identity_OpenId_Playground>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(openIdDb)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints();

// A SECOND, conformance-only OP instance: the same Playground project launched under the Conformance environment
// (the ConformanceAspire launch profile, ASPNETCORE_ENVIRONMENT=Conformance) on its own ports
// (https://localhost:5003, http://localhost:5002), sharing the one Postgres. Its appsettings.Conformance.json overlay
// defines its own OpenId:Server:Settings root ceiling that advertises the available-but-off-by-default capabilities
// (Session Management, Front-Channel Logout, the client_credentials grant — ADR-0088) so the conformance dashboard
// reports them, while the `api` above stays the locked-down secure default. The same overlay sets Cors:AllowedOrigins
// (the Development CORS default does not apply outside Development) so the 5175 dashboard can read its discovery
// cross-origin. WaitForCompletion(migrations) holds it until the shared schema is applied.
var conformanceApi = builder
    .AddProject<Projects.NCode_Identity_OpenId_Playground>(
        "conformanceapi",
        launchProfileName: "ConformanceAspire"
    )
    .WithHttpHealthCheck("/health")
    .WithReference(openIdDb)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints();

// The interactive login/consent surface (apps/auth) is a BACKEND-SERVED bundle: its `dev` script is `vite build
// --watch`, which continuously rebuilds `dist` for the Playground to serve under /account/* (ADR-0065). It is a
// build-watcher with no HTTP endpoint of its own, so it is modeled as a plain JavaScript app resource, not a Vite dev
// server. The Playground serves whatever `dist` exists; on a cold start give the first build a moment to finish.
// `install: false` — the pnpm workspace is installed ONCE out-of-band by build/frontend-install.ps1; letting each app
// auto-install would run two `pnpm install` processes concurrently against the shared, symlinked node_modules and
// collide (ERR_PNPM_EBUSY).
builder.AddJavaScriptApp("auth", "../frontend/apps/auth").WithPnpm(install: false);

// The Management console (apps/mgmt) is a STANDALONE Vite dev server. It is pinned to http://localhost:5173 (direct,
// un-proxied, so HMR and the OAuth authorization-code callback work) because the developer seed registers
// https://localhost:5173/callback as the SPA redirect URI and AddDeveloperCors allowlists localhost:5173. The browser
// SPA discovers the API from the host-published bootstrap document (ADR-0064); because this bundle is NOT served by the
// OP host, its Vite dev server proxies /config/bootstrap.json to the api so local dev consumes the real runtime
// endpoint instead of a static copy. WithReference injects the service-discovery variable that proxy targets AND
// exposes Aspire service discovery to the Vite process; WaitFor holds startup until the API is healthy so the first
// discovery/management call cannot race the API.
builder
    .AddViteApp("mgmt", "../frontend/apps/mgmt")
    .WithPnpm(install: false)
    .WithEndpoint(
        "http",
        endpoint =>
        {
            endpoint.Port = 5173;
            endpoint.TargetPort = 5173;
            endpoint.IsProxied = false;
        },
        createIfNotExists: false
    )
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

// The Playground flow explorer (apps/playground) is a STANDALONE Vite dev server pinned to http://localhost:5174 (the
// port the seeded `playground-spa` redirect URI and AddDeveloperCors allowlist assume). It drives a live
// authorization-code + PKCE round trip against the OP, so — like `mgmt` — it has its own seeded public client and CORS
// origin, and proxies /config/bootstrap.json to the api, discovering its `playground` surface coordinates at boot
// (ADR-0064). The same un-proxied fixed-port rules as `mgmt` apply so the OAuth redirect callback resolves.
builder
    .AddViteApp("playground", "../frontend/apps/playground")
    .WithPnpm(install: false)
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
// http://localhost:5175. It only audits the OP's published discovery metadata, so it needs no OAuth client; it proxies
// /config/bootstrap.json to the dedicated conformance OP (`conformanceapi`, which advertises the opt-in capabilities)
// to learn its issuer, then reads that issuer's discovery cross-origin (allowlisted by the Conformance overlay's
// Cors:AllowedOrigins). WaitFor holds startup until that OP is healthy so the first discovery read cannot race it.
builder
    .AddViteApp("conformance", "../frontend/apps/conformance")
    .WithPnpm(install: false)
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

return;

static void EnsureNvsNodeOnPath()
{
    if (!OperatingSystem.IsWindows())
        return;

    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var nvsNodeRoot = Path.Combine(localAppData, "nvs", "node");
    if (!Directory.Exists(nvsNodeRoot))
        return;

    // NVS lays each install out as <root>/node/<version>/<arch>; take the newest Node 22 x64 bin directory. Corepack
    // (bundled with Node) places the `pnpm` shim here once `corepack enable` has run (build/frontend-install.ps1).
    var nodeDir = Directory
        .EnumerateDirectories(nvsNodeRoot, "22*")
        .Select(versionDir => Path.Combine(versionDir, "x64"))
        .Where(Directory.Exists)
        .OrderByDescending(dir => dir, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
    if (nodeDir is null)
        return;

    var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    if (path.Split(Path.PathSeparator).Contains(nodeDir, StringComparer.OrdinalIgnoreCase))
        return;

    Environment.SetEnvironmentVariable("PATH", $"{nodeDir}{Path.PathSeparator}{path}");
}
