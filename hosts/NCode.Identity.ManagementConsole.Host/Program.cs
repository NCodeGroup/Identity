// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.AspNetCore;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.ManagementConsole;
using NCode.Identity.Secrets.AwsKms;
using NCode.Identity.Secrets.AzureKeyVault;
using NCode.Identity.Secrets.GoogleCloudKms;
using NCode.Identity.SharedState.Valkey;

// COMPOSITION-ONLY HOST: this file is a flat list of turnkey Add*/Use*/Map* calls. Reusable or
// developer-only startup logic does NOT belong here — put it in the owning package as an extension
// (the console's server lives in NCode.Identity.ManagementConsole). If a step needs an `if`, a `foreach`,
// a policy builder, or config parsing, move it to a package extension. See csharp-production.instructions.md §4.
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger();

var managementConsole = builder.Services.AddManagementConsole(builder.Configuration);
managementConsole.IdentityBffBuilder.SharedStateLibraryBuilder.AddValkey(builder.Configuration);

// AWS KMS can hold the console's client, decryption and DPoP keys (Bff:KeyRings:{name}:RemoteKeys). The provider
// reads each key where its ARN names, so it registers without a region and creates no key.
managementConsole.IdentityBffBuilder.SecretsLibraryBuilder.AddAwsKmsRemoteKeyProvider(
    builder.Configuration
);

// Azure Key Vault or Managed HSM can hold the console's client, decryption and DPoP keys
// (Bff:KeyRings:{name}:RemoteKeys). The provider reads each key where its key identifier names, so it registers
// without a vault URI and creates no key.
managementConsole.IdentityBffBuilder.SecretsLibraryBuilder.AddAzureKeyVaultRemoteKeyProvider(
    builder.Configuration
);

// Google Cloud KMS can hold the console's client, decryption and DPoP keys (Bff:KeyRings:{name}:RemoteKeys). The
// provider reads each key where its key version name names, so it registers without a key ring and creates no key.
managementConsole.IdentityBffBuilder.SecretsLibraryBuilder.AddGoogleCloudKmsRemoteKeyProvider(
    builder.Configuration
);

// Trust X-Forwarded-* headers only from the proxies in the 'ForwardedHeaders' configuration section.
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);

// Send Strict-Transport-Security as configured by the 'StrictTransportSecurity' configuration section.
builder.Services.AddStrictTransportSecurity(builder.Configuration);

// Send no Server response header, in every environment. See ADR-1511.
builder.Services.AddServerHeaderSuppression();

// In Production, refuse to start unless Kestrel serves HTTPS only with a certificate that loads and passes the
// transport checks (ADR-1504). Inert in every other environment.
builder.Services.AddTransportSecurity(builder.Environment);

// The pods can require the reverse proxy's client certificate on the proxy-to-pod hop, as configured by the
// 'ProxyClientCertificate' section (ADR-1506). Inert unless 'ProxyClientCertificate:Required' is true.
builder.Services.AddProxyClientCertificate(builder.Configuration);

// Request the proxy's client certificate on Kestrel's HTTPS endpoints when 'ProxyClientCertificate:Required' is true.
var proxyClientCertificate = builder.Configuration.GetProxyClientCertificateOptions();
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ConfigureHttpsDefaults(https =>
        https.RequestProxyClientCertificate(proxyClientCertificate)
    )
);

builder.Services.AddHealthChecks();

var app = builder.Build();

// MUST be the first middleware: it refuses a request whose connection does not carry the reverse proxy's client
// certificate (except the liveness and readiness probes) before anything else reads it. A no-op unless
// 'ProxyClientCertificate:Required' is true (ADR-1506).
app.UseProxyClientCertificate();

// Right after the proxy check: everything downstream that reads the scheme or host observes the original request
// rather than the proxy-to-application hop.
app.UseTrustedForwardedHeaders();
app.UseStrictTransportSecurity();
app.UseHttpsRedirectionOutsideProduction();
app.UseBffSecurityHeaders();
app.UseBff();
app.UseAuthentication();
app.UseAuthorization();

app.MapManagementConsole();

// Liveness runs no checks, so a dependency outage never restarts a replica.
// Readiness runs the checks tagged ready.
app.MapHealthProbes();

await app.RunAsync();
