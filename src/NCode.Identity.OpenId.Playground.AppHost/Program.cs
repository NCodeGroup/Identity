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

// The reference OpenID Provider host. Its default launch profile supplies the Development environment variables and
// the fixed dev endpoints (https://localhost:5001, http://localhost:5000) the rest of the stack is pinned to — the
// issuer, the browser-published bootstrap document, the seeded SPA redirect URIs, and the developer CORS origins all
// assume those ports. WithHttpHealthCheck gates dependents on the app being HEALTHY (its /health returns 200 once
// Kestrel is listening), not merely "process started", which is what deterministically removes the UI-before-API race.
var api = builder
    .AddProject<Projects.NCode_Identity_OpenId_Playground>("api")
    .WithHttpHealthCheck("/health")
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
// SPA discovers the API from the host-published bootstrap document (ADR-0064), which points at the pinned api origin;
// WithReference also exposes Aspire service discovery to the Vite process, and WaitFor holds startup until the API is
// healthy so the first discovery/management call cannot race the API.
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
