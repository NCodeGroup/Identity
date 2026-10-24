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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using NCode.Identity;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Host.Database;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;
using NCode.Identity.Secrets.Persistence.AwsKms;
using NCode.Identity.Secrets.Persistence.AzureKeyVault;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms;
using NCode.Identity.Server;
using NCode.Identity.SharedState.Valkey;
using NCode.Registration.AspNetCore;

// COMPOSITION-ONLY HOST: this file is a flat list of turnkey Add*/Use*/Map* calls. Reusable or
// developer-only startup logic does NOT belong here — put it in the owning package as an extension
// (dev helpers live in NCode.Identity.Server/DevelopmentEnvironment, e.g. AddDeveloperCors). If a step
// needs an `if`, a `foreach`, a policy builder, or config parsing, move it to a package extension.
// See csharp-production.instructions.md §4.
var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var hostEnvironment = builder.Environment;
var serviceCollection = builder.Services;

// Logs go to standard output on every platform: the console provider writes them, and the EventSource provider hands
// them to a diagnostics tool that attaches (dotnet-monitor, dotnet-trace). The default builder's Windows Event Log and
// debugger providers are left out. See ADR-0210.
builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger();

// SAFETY GATE: refuse to start a Production deployment that still has development-only conveniences enabled
// (developer signing keys, a wildcard host filter, developer CORS configuration, seeded accounts). Every one of them is
// silent — the server would start, serve traffic, and look healthy — so this fails before the port is bound.
// Inert in every non-Production environment.
serviceCollection.AddProductionSafety(configuration, hostEnvironment);

serviceCollection.AddAntiforgery();

serviceCollection.AddHealthChecks();

// Metrics: the family's meters plus ASP.NET Core, Kestrel, outbound HTTP, and the runtime, exported over OTLP when
// OTEL_EXPORTER_OTLP_ENDPOINT is configured (the Aspire AppHost injects it, so its dashboard shows them). ADR-0197.
serviceCollection.AddOpenTelemetryMetrics(configuration);

serviceCollection.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
});

// Trust a reverse proxy's X-Forwarded-* headers, but only the proxies named in the 'ReverseProxy' configuration
// section. Inert when none are configured, which is the correct posture for a direct-TLS deployment.
serviceCollection.AddReverseProxy(configuration);

// Receive mutual-TLS client certificates when this host terminates TLS itself: an endpoint configured to request one
// (Kestrel:EndpointDefaults:ClientCertificateMode) accepts any certificate at the handshake, and the client-
// authentication handlers and certificate-bound tokens validate it (RFC 8705, ADR-0202). Kestrel's own validation
// would refuse every self-signed or tenant-PKI certificate first. Inert while no endpoint requests one. The host owns
// this hook: ConfigureHttpsDefaults keeps only the last action, so every HTTPS default belongs in this one call.
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureHttpsDefaults(https => https.AllowAnyClientCertificate())
);

var openIdOptionsSectionName = Environment.GetEnvironmentVariable("OpenId_OptionsSectionName");

// TODO
var identityServer = serviceCollection.AddIdentityServer();
identityServer.AddConfiguration(configuration, openIdOptionsSectionName);

// AWS KMS can hold the root key ('SecretProtection:RootKeyProvider' = 'aws-kms'); its client is created on first use, so the provider is inert otherwise.
identityServer.SecretPersistenceLibraryBuilder.AddAwsKmsRootKeyProvider(configuration);

// Azure Key Vault or Managed HSM can hold the root key ('SecretProtection:RootKeyProvider' = 'azure-key-vault'); its clients are created on first use, so the provider is inert otherwise.
identityServer.SecretPersistenceLibraryBuilder.AddAzureKeyVaultRootKeyProvider(configuration);

// Google Cloud KMS can hold the root key ('SecretProtection:RootKeyProvider' = 'google-cloud-kms'); its client is created on first use, so the provider is inert otherwise.
identityServer.SecretPersistenceLibraryBuilder.AddGoogleCloudKmsRootKeyProvider(configuration);

// Bind the frontend runtime bootstrap document (ADR-0064) from the 'Frontend' configuration section so the host can
// publish this deployment's coordinates (API base, issuer, client id, feature flags, branding) to the tier-agnostic
// bundles via MapFrontendBootstrap below.
identityServer.AddFrontendBootstrap(configuration);

// Shared state every replica must see — DPoP nonces and the single-use records of DPoP proofs and client assertions —
// goes to Valkey when the 'valkey' connection string is configured (the AppHost injects it), and otherwise stays
// in-process, which is correct only for a single replica (ADR-0164).
identityServer.SharedStateLibraryBuilder.AddValkey(configuration);

var entityFrameworkPersistence =
    serviceCollection.AddEntityFrameworkCorePersistence<OpenIdDbContext>();

// Opt in to the generic Entity Framework local-account reference store (the server-owned credential, profile, and
// status payload behind self-issued connections). A host that fronts its own user store would register a different
// ILocalAccountStore instead (for example, the ASP.NET Core Identity adapter). The store slice requires the persistence
// framework's builder as its receiver, and returns the compile-time proof the account capabilities require (ADR-0069).
var localAccountStore = entityFrameworkPersistence.AddLocalAccountEntityFramework();

// Enable the OpenId Authentication capabilities on the authentication-library builder (ADR-0069 compile-time marker
// prerequisite): the resource-owner password grant, the interactive login/consent surface (a cookie scheme plus the
// /account/* endpoints that serve the apps/auth bundle), and the local-account implementation of the surface's
// credential verifier. All reuse the configured local-account store (dependency inversion — the Authentication library
// itself does not depend on accounts) so the authorize flow completes end-to-end.
identityServer
    .OpenIdAuthenticationLibraryBuilder.AddLocalAccountAuthentication(localAccountStore)
    .AddInteractiveAuthentication(configuration, hostEnvironment)
    .AddLocalAccountInteractiveAuthentication(localAccountStore);

// Enable the local-account management endpoints on the management-library builder (nested under the tenant-plane
// group). The account stack provides this capability; the Management slice itself does not depend on accounts.
identityServer.OpenIdManagementLibraryBuilder.AddLocalAccountManagement(localAccountStore);

// DEVELOPMENT CONVENIENCE: everything that is only correct on a developer's machine — the Vite CORS policy, the
// developer and conformance seeders, and the developer signing keys — behind one self-gating call that does nothing
// in Production. Each piece also stays inert unless its own configuration section is present.
identityServer.AddDevelopmentEnvironment(configuration, hostEnvironment);

serviceCollection.AddDatabaseDeveloperPageExceptionFilter();

// The dev database is Postgres, orchestrated by the Aspire AppHost (ADR-0073): the AppHost provisions the container
// and injects the 'openiddb' connection string, and a dedicated migration worker applies the schema before this host
// starts — so the api never bootstraps the database itself. The developer data-protection key ring is persisted in the
// same database, so one purge resets the OpenID data, the seeded signing key, the tenants' master keys, and the ring
// together. Stored secrets are encrypted under their owners' master keys and the ring under the server master key,
// whose root key lives outside the database (the 'SecretProtection' section); the production gate refuses to start
// without a real root key (ADR-0235).

var connectionString = configuration.GetConnectionString("openiddb");
if (!string.IsNullOrEmpty(connectionString))
{
    serviceCollection.AddDbContextFactory<OpenIdDbContext>(dbOptions =>
        dbOptions.UseOpenIdNpgsql(connectionString)
    );
    serviceCollection.AddDbContext<DeveloperDataProtectionDbContext>(dbOptions =>
        dbOptions.UseDataProtectionNpgsql(connectionString)
    );
    serviceCollection
        .AddDataProtection()
        .PersistKeysToDbContext<DeveloperDataProtectionDbContext>()
        .SetApplicationName("NCode.Identity.OpenId.Host");

    // The replica is ready only while it can reach the database it serves every request from.
    serviceCollection
        .AddHealthChecks()
        .AddDbContextCheck<OpenIdDbContext>("openiddb", tags: [HealthCheckTags.Readiness]);
}
else
{
    // No connection string: hermetic test hosts boot this composition root and override the context with an isolated
    // in-memory store. Default (ephemeral) data protection is sufficient because tests use ephemeral signing keys.
    serviceCollection.AddDbContextFactory<OpenIdDbContext>(dbOptions =>
        dbOptions.UseInMemoryDatabase("OpenId")
    );
}

await using var app = builder.Build();

// MUST be the first middleware: everything downstream that reads the scheme or host — the HTTPS redirect, request
// logging's client IP, and the tenant's base address — has to observe the original request rather than the
// proxy-to-application hop. A no-op unless 'ReverseProxy' names a trusted proxy.
app.UseReverseProxy();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Turnkey OpenAPI document + Scalar API reference, owned by the composition layer (prod and dev).
app.MapIdentityServerApiReference();

app.UseHttpLogging();
app.UseHttpsRedirection();

app.UseRouting();

// Must run after UseRouting and before the authN/authZ + endpoint middleware. A no-op unless AddDeveloperCors
// registered the developer policy, which AddDevelopmentEnvironment never does in Production.
app.UseDeveloperCors();

app.UseAuthentication();
app.UseAuthorization();

// UseAntiforgery MUST run after UseAuthentication/UseAuthorization so the antiforgery middleware
// validates the token against the SAME HttpContext.User that was authenticated for the request. The
// interactive login tokens are claims-bound: the login page (GET) mints the token after authentication
// (so against the signed-in user when an Identity.Application cookie already exists), and placing this
// before UseAuthentication would validate the POST against an anonymous user — failing with
// "The provided antiforgery token was meant for a different claims-based user than the current user"
// on every login after the first.
app.UseAntiforgery();

app.MapEndpointGroups();

// Liveness runs no checks: it reports only that the process answers, so a dependency outage never restarts a replica.
// Readiness runs the checks tagged 'ready' (the database, and Valkey when configured) and takes the replica out of
// rotation while a dependency it needs is unreachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .WithName("health_live_endpoint");
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckTags.Readiness),
        }
    )
    .WithName("health_ready_endpoint");

await app.RunAsync();
