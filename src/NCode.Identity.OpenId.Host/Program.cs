// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Security;
using System.Security.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using NCode.AspNetCore;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Host.Database;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;
using NCode.Identity.Secrets.AwsKms;
using NCode.Identity.Secrets.AzureKeyVault;
using NCode.Identity.Secrets.GoogleCloudKms;
using NCode.Identity.Server;
using NCode.Identity.Server.SecretProtection;
using NCode.Identity.SharedState.Valkey;
using NCode.Registration;
using NCode.Registration.AspNetCore;

// COMPOSITION-ONLY HOST: this file is a flat list of turnkey Add*/Use*/Map* calls. Reusable or
// developer-only startup logic does NOT belong here — put it in the owning package as an extension
// (dev helpers live in NCode.Identity.Server/DevelopmentEnvironment, e.g. AddDeveloperCors). If a step
// needs an `if`, a `foreach`, a policy builder, or config parsing, move it to a package extension.
// See csharp-production.instructions.md §4. The offline command's dispatch below is the one exception: only the entry
// point can return its exit code.

// OFFLINE COMMAND: 'secret-protection rewrap|rewrap-recovery' rewraps the master keys and exits without serving, for a
// one-off Job that runs this image (ADR-1408). The host is built without the command's arguments, which a command-line
// configuration provider would read as configuration.
var secretProtectionCommand = SecretProtectionCommandLine.Parse(args);
var builder = WebApplication.CreateBuilder(SecretProtectionCommandLine.GetHostArguments(args));

var configuration = builder.Configuration;
var hostEnvironment = builder.Environment;
var serviceCollection = builder.Services;

// Logs go to standard output on every platform: the console provider writes them, and the EventSource provider hands
// them to a diagnostics tool that attaches (dotnet-monitor, dotnet-trace). The default builder's Windows Event Log and
// debugger providers are left out. See project-template ADR-0115.
builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger();

// SAFETY GATE: refuse to start a Production deployment that still has development-only conveniences enabled
// (developer signing keys, a wildcard host filter, developer CORS configuration, seeded accounts). Every one of them is
// silent — the server would start, serve traffic, and look healthy — so this fails before the port is bound.
// Inert in every non-Production environment.
serviceCollection.AddProductionSafety(configuration, hostEnvironment);

serviceCollection.AddAntiforgery();

serviceCollection.AddHealthChecks();

// Metrics: the family's meters plus ASP.NET Core, Kestrel, outbound HTTP, and the runtime, exported over OTLP when
// OTEL_EXPORTER_OTLP_ENDPOINT is configured (the Aspire AppHost injects it, so its dashboard shows them). ADR-1197.
serviceCollection.AddOpenTelemetryMetrics(configuration);

serviceCollection.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
});

// Trust a reverse proxy's X-Forwarded-* headers, but only the proxies named in the 'ForwardedHeaders' configuration
// section. Inert when none are configured, which is the correct posture for a direct-TLS deployment.
serviceCollection.AddTrustedForwardedHeaders(configuration);

// Send Strict-Transport-Security on every HTTPS response outside Development, as configured by the
// 'StrictTransportSecurity' section (one year with includeSubDomains by default). A deployment whose TLS-terminating
// proxy sends the header sets StrictTransportSecurity:Enabled to false. See ADR-1258.
serviceCollection.AddStrictTransportSecurity(configuration);

// Receive mutual-TLS client certificates when this host terminates TLS itself: an endpoint configured to request one
// (Kestrel:EndpointDefaults:ClientCertificateMode) accepts any certificate at the handshake, and the client-
// authentication handlers and certificate-bound tokens validate it (RFC 8705, ADR-1202). Kestrel's own validation
// would refuse every self-signed or tenant-PKI certificate first. Inert while no endpoint requests one. The host owns
// this hook: ConfigureHttpsDefaults keeps only the last action, so every HTTPS default belongs in this one call. The
// handshake also accepts only TLS 1.2 and 1.3 (FAPI 1.0 Part 2 §8.5, RFC 8996), whatever the operating system enables,
// and only AEAD cipher suites with forward secrecy where the platform lets a process choose them (SChannel on Windows
// does not).
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureHttpsDefaults(https =>
    {
        https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        https.AllowAnyClientCertificate();
        if (!OperatingSystem.IsWindows())
        {
            https.OnAuthenticate = (_, sslOptions) =>
                sslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(
                    [
                        TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                        TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
                        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
                        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
                    ]
                );
        }
    })
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

// DEVELOPMENT CONVENIENCE: everything that is only correct on a developer's machine — the Vite CORS policy, the
// developer and conformance seeders, and the developer key pruning — behind one call. Each piece stays inert unless
// its own configuration section is present, and AddProductionSafety refuses each of those sections in Production.
identityServer.AddDevelopmentEnvironment(configuration);

serviceCollection.AddDatabaseDeveloperPageExceptionFilter();

// The dev database is Postgres, orchestrated by the Aspire AppHost (ADR-1073): the AppHost provisions the container
// and injects the 'openiddb' connection string, and a dedicated migration worker applies the schema before this host
// starts — so the api never bootstraps the database itself. The developer data-protection key ring is persisted in the
// same database, so one purge resets the OpenID data, the seeded signing key, the tenants' master keys, and the ring
// together. Stored secrets are encrypted under their owners' master keys and the ring under the platform master key,
// whose root key lives outside the database (the 'SecretProtection' section); the production gate refuses to start
// without a real root key (ADR-1235).

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
    // No connection string: hermetic test hosts boot this composition root and replace the OpenID context and the Data
    // Protection key ring with stores of their own; Data Protection otherwise keeps its default repository, which the
    // production gate refuses.
    serviceCollection.AddDbContextFactory<OpenIdDbContext>(dbOptions =>
        dbOptions.UseInMemoryDatabase("OpenId")
    );
}

await using var app = builder.Build();

// The command runs against the built services, never starts the hosted services or binds a port, and returns its exit
// code. It is the one branch this file keeps, because only the entry point can return an exit code.
if (secretProtectionCommand is not null)
    return await secretProtectionCommand.RunAsync(app.Services, CancellationToken.None);

// MUST be the first middleware: everything downstream that reads the scheme or host — the HTTPS redirect, request
// logging's client IP, and the tenant's base address — has to observe the original request rather than the
// proxy-to-application hop. A no-op unless 'ForwardedHeaders' names a trusted proxy.
app.UseTrustedForwardedHeaders();

// Right after the forwarded headers, so a request a trusted proxy received over HTTPS counts as HTTPS. A no-op in
// Development. See ADR-1258.
app.UseStrictTransportSecurity();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Turnkey OpenAPI document + Scalar API reference, owned by the composition layer (prod and dev).
app.MapIdentityServerApiReference();

app.UseHttpLogging();
app.UseHttpsRedirection();

app.UseRouting();

// Must run after UseRouting and before the authN/authZ + endpoint middleware. A no-op unless the configuration has a
// Cors section, which AddProductionSafety refuses in Production.
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
