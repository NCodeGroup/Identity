// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Host.Database;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;
using NCode.Identity.OpenId.Setup;
using NCode.Identity.Registration;
using NCode.Identity.Registration.AspNetCore;
using NCode.Identity.Secrets.AwsKms;
using NCode.Identity.Secrets.AzureKeyVault;
using NCode.Identity.Secrets.GoogleCloudKms;
using NCode.Identity.Server;
using NCode.Identity.Server.InstanceSetup;
using NCode.Identity.Server.SecretProtection;
using NCode.Identity.SharedState.Valkey;

// COMPOSITION-ONLY HOST: this file is a flat list of turnkey Add*/Use*/Map* calls. Reusable or
// developer-only startup logic does NOT belong here — put it in the owning package as an extension
// (dev helpers live in NCode.Identity.Server/DevelopmentEnvironment, e.g. AddDeveloperCors). If a step
// needs an `if`, a `foreach`, a policy builder, or config parsing, move it to a package extension.
// See csharp-production.instructions.md §4. The offline commands' dispatch below is the one exception: only the entry
// point can return an exit code.

// OFFLINE COMMANDS: 'secret-protection rewrap|rewrap-recovery' rewraps the master keys (ADR-1408), and 'setup' creates
// the root administrator of an uninitialized instance from the 'Setup:Root' configuration (ADR-1512). Each exits without
// serving, for a one-off Job that runs this image. The host is built without a command's arguments, which a
// command-line configuration provider would read as configuration.
var secretProtectionCommand = SecretProtectionCommandLine.Parse(args);
var instanceSetupCommand = InstanceSetupCommandLine.Parse(args);
var builder = WebApplication.CreateBuilder(
    InstanceSetupCommandLine.GetHostArguments(SecretProtectionCommandLine.GetHostArguments(args))
);

var configuration = builder.Configuration;
var hostEnvironment = builder.Environment;
var serviceCollection = builder.Services;

// Logs go to standard output on every platform: the console provider writes them, and the EventSource provider hands
// them to a diagnostics tool that attaches (dotnet-monitor, dotnet-trace). The default builder's Windows Event Log and
// debugger providers are left out. See project-template ADR-0115.
builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger();

// SAFETY GATE: refuse to start a deployment that still has development-only conveniences enabled
// (developer signing keys, a wildcard host filter, developer CORS configuration, seeded accounts) or that does not serve
// HTTPS only with a usable certificate. Every one of them is silent — the server would start, serve traffic, and look
// healthy — so the configuration checks fail before the port is bound and the transport check fails as the host
// finishes starting, before any probe passes.
// Runs in every host environment; a local inner-loop host turns it off with 'LocalDevelopment:Enabled'.
serviceCollection.AddDeploymentSafety(configuration);

serviceCollection.AddAntiforgery();

serviceCollection.AddHealthChecks();

// Metrics: the family's meters plus ASP.NET Core, Kestrel, outbound HTTP, and the runtime, exported over OTLP when
// OTEL_EXPORTER_OTLP_ENDPOINT is configured (the Aspire AppHost injects it, so its dashboard shows them). ADR-1197.
serviceCollection.AddOpenTelemetryMetrics(configuration);

// HTTP logging writes the request and response properties, the allow-listed headers and the duration, never a query
// string or a credential header value; an endpoint opts in to its own body in code, and the protocol, management and
// interactive endpoints refuse it, so no log level writes a credential (ADR-1503).
serviceCollection.AddGuardedHttpLogging();

// Trust a reverse proxy's X-Forwarded-* headers, but only the proxies named in the 'ForwardedHeaders' configuration
// section. Inert when none are configured, which is the correct posture for a direct-TLS deployment.
serviceCollection.AddTrustedForwardedHeaders(configuration);

// The pods can require the reverse proxy's client certificate on the proxy-to-pod hop, as configured by the
// 'ProxyClientCertificate' section (ADR-1506). Inert unless 'ProxyClientCertificate:Required' is true.
serviceCollection.AddProxyClientCertificate(configuration);

// Send Strict-Transport-Security on every HTTPS response outside Development, as configured by the
// 'StrictTransportSecurity' section (one year with includeSubDomains by default). A deployment whose TLS-terminating
// proxy sends the header sets StrictTransportSecurity:Enabled to false. See ADR-1447.
serviceCollection.AddStrictTransportSecurity(configuration);

// Send no Server response header, in every environment. See ADR-1511.
serviceCollection.AddServerHeaderSuppression();

// Receive mutual-TLS client certificates when this host terminates TLS itself: an endpoint configured to request one
// (Kestrel:EndpointDefaults:ClientCertificateMode) accepts any certificate at the handshake, and the client-
// authentication handlers and certificate-bound tokens validate it (RFC 8705, ADR-1202). Kestrel's own validation
// would refuse every self-signed or tenant-PKI certificate first. Inert while no endpoint requests one. The host owns
// this hook: ConfigureHttpsDefaults keeps only the last action, so every HTTPS default belongs in this one call, which
// HostHttpsDefaults holds. The handshake also accepts only TLS 1.2 and 1.3, and only AEAD cipher suites with forward
// secrecy where the platform lets a process choose them (ADR-1499).
var proxyClientCertificate = configuration.GetProxyClientCertificateOptions();
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureHttpsDefaults(https =>
        HostHttpsDefaults.Configure(https, proxyClientCertificate)
    )
);

var openIdOptionsSectionName = Environment.GetEnvironmentVariable("OpenId_OptionsSectionName");

var identityServer = serviceCollection.AddIdentityServer();
identityServer.AddConfiguration(configuration, openIdOptionsSectionName);

// AWS KMS can hold the root key ('SecretProtection:RootKeyProvider' = 'aws-kms'); its client is created on first use, so the provider is inert otherwise.
identityServer.SecretPersistenceLibraryBuilder.AddAwsKmsKeyWrapper(configuration);

// Azure Key Vault or Managed HSM can hold the root key ('SecretProtection:RootKeyProvider' = 'azure-key-vault'); its clients are created on first use, so the provider is inert otherwise.
identityServer.SecretPersistenceLibraryBuilder.AddAzureKeyVaultKeyWrapper(configuration);

// Google Cloud KMS can hold the root key ('SecretProtection:RootKeyProvider' = 'google-cloud-kms'); its client is created on first use, so the provider is inert otherwise.
identityServer.SecretPersistenceLibraryBuilder.AddGoogleCloudKmsKeyWrapper(configuration);

// AWS KMS can hold a tenant's keys ('tenant_key_provider' = 'aws-kms'); it is registered only when the 'SecretProtection:RemoteKeyProviders:AwsKms' section exists, and without its location it binds keys and creates none.
identityServer.SecretsLibraryBuilder.AddAwsKmsRemoteKeyProviderIfConfigured(configuration);

// Azure Key Vault or Managed HSM can hold a tenant's keys ('tenant_key_provider' = 'azure-key-vault'); it is registered only when the 'SecretProtection:RemoteKeyProviders:AzureKeyVault' section exists, and without its location it binds keys and creates none.
identityServer.SecretsLibraryBuilder.AddAzureKeyVaultRemoteKeyProviderIfConfigured(configuration);

// Google Cloud KMS can hold a tenant's keys ('tenant_key_provider' = 'google-cloud-kms'); it is registered only when the 'SecretProtection:RemoteKeyProviders:GoogleCloudKms' section exists, and without its location it binds keys and creates none.
identityServer.SecretsLibraryBuilder.AddGoogleCloudKmsRemoteKeyProviderIfConfigured(configuration);

// Bind the frontend runtime bootstrap document (ADR-1064) from the 'Frontend' configuration section so the host can
// publish this deployment's coordinates (API base, issuer, client id, feature flags, branding) to the tier-agnostic
// bundles via MapFrontendBootstrap below.
identityServer.AddFrontendBootstrap(configuration);

// Shared state every replica must see — DPoP nonces and the single-use records of DPoP proofs and client assertions —
// goes to Valkey when the 'valkey' connection string is configured (the AppHost injects it), and otherwise stays
// in-process, which is correct only for a single replica (ADR-1164).
identityServer.SharedStateLibraryBuilder.AddValkey(configuration);

var entityFrameworkPersistence =
    serviceCollection.AddEntityFrameworkCorePersistence<OpenIdDbContext>();

// Opt in to the generic Entity Framework local-account reference store (the server-owned credential, profile, and
// status payload behind self-issued connections). A host that fronts its own user store would register a different
// ILocalAccountStore instead (for example, the ASP.NET Core Identity adapter). The store slice requires the persistence
// framework's builder as its receiver, and returns the compile-time proof the account capabilities require (ADR-1069).
var localAccountStore = entityFrameworkPersistence.AddLocalAccountEntityFramework();

// The local-account store hashes and verifies passwords with the IPasswordHasher the host registers.
serviceCollection.AddDefaultPasswordHasher();

// Enable the OpenId Authentication capabilities on the authentication-library builder (ADR-1069 compile-time marker
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

// Enable the one-time instance setup: the '/setup' wizard and its API, and the 'setup' command, which create the root
// administrator of an uninitialized instance guarded by the 'Setup:Token' configuration or a generated one-time token.
// Every '/setup' route answers 404 once the instance is initialized (ADR-1512).
identityServer.OpenIdManagementLibraryBuilder.AddInstanceSetup(configuration, hostEnvironment);

// DEVELOPMENT CONVENIENCE: everything that is only correct on a developer's machine — the Vite CORS policy, the
// developer and conformance seeders, and the developer key pruning — behind one call. Each piece stays inert unless
// its own configuration section is present, and AddDeploymentSafety refuses each of those sections in a deployment.
identityServer.AddDevelopmentEnvironment(configuration);

serviceCollection.AddDatabaseDeveloperPageExceptionFilter();

// The dev database is Postgres, orchestrated by the Aspire AppHost (ADR-1073): the AppHost provisions the container
// and injects the 'openiddb' connection string (or an operator's Secret supplies the 'OpenIdDatabase' parts, ADR-1509),
// and a dedicated migration worker applies the schema before this host starts — so the api never bootstraps the
// database itself. The developer data-protection key ring is persisted in the same database, so one purge resets the
// OpenID data, the seeded signing key, the tenants' master keys, and the ring together. Stored secrets are encrypted
// under their owners' master keys and the ring under the platform master key, whose root key lives outside the database
// (the 'SecretProtection' section); the production gate refuses to start without a real root key (ADR-1235).

var connectionString = HostDatabaseConnection.Resolve(configuration);
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
    // No connection string: hermetic test hosts boot this composition root and replace the OpenID context and the Data
    // Protection key ring with stores of their own; Data Protection otherwise keeps its default repository, which the
    // production gate refuses.
    serviceCollection.AddDbContextFactory<OpenIdDbContext>(dbOptions =>
        dbOptions.UseInMemoryDatabase("OpenId")
    );
}

await using var app = builder.Build();

// A command runs against the built services, never starts the hosted services or binds a port, and returns its exit
// code. These are the only branches this file keeps, because only the entry point can return an exit code.
if (secretProtectionCommand is not null)
    return await secretProtectionCommand.RunAsync(app.Services, CancellationToken.None);

if (instanceSetupCommand is not null)
    return await instanceSetupCommand.RunAsync(app.Services, CancellationToken.None);

// MUST be the first middleware, before even the forwarded headers: it refuses a request whose connection does not carry
// the reverse proxy's client certificate (except the liveness and readiness probes) before anything else reads it. A
// no-op unless 'ProxyClientCertificate:Required' is true (ADR-1506).
app.UseProxyClientCertificate();

// Right after the proxy check: everything downstream that reads the scheme, host or client address — the HTTPS
// redirect, host-based tenant selection, the choice among the tenant's own origins, the per-address rate limit, the
// failed-client-authentication limit, the audit record's source address and the session's remote address — has to
// observe the original request rather than the proxy-to-application hop. A no-op unless 'ForwardedHeaders' names a
// trusted proxy.
app.UseTrustedForwardedHeaders();

// Right after the forwarded headers, so a request a trusted proxy received over HTTPS counts as HTTPS. A no-op in
// Development. See ADR-1447.
app.UseStrictTransportSecurity();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// The OpenAPI document and the Scalar API reference page, mapped only where the ApiReference section enables them.
// Both are off by default and appsettings.Development.json turns both on (ADR-1481).
app.MapIdentityServerApiReference();

// A deployment binds HTTPS endpoints only (AddDeploymentSafety refuses to start otherwise), so it adds no redirect;
// a host with 'LocalDevelopment:Enabled' set keeps it.
app.UseLocalDevelopmentHttpsRedirection();

app.UseRouting();

// After routing, so HTTP logging sees the selected endpoint and writes a body only for one that opts in to it.
app.UseHttpLogging();

// Must run after UseRouting and before the authN/authZ + endpoint middleware. A no-op unless the configuration has a
// Cors section, which AddDeploymentSafety refuses in a deployment.
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

// Liveness runs no checks, so a dependency outage never restarts a replica.
// Readiness runs the checks tagged ready.
app.MapHealthProbes();

await app.RunAsync();

return 0;
