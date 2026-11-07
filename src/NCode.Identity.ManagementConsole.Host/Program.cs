// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.AspNetCore;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.ManagementConsole;
using NCode.Identity.SharedState.Valkey;

// COMPOSITION-ONLY HOST: this file is a flat list of turnkey Add*/Use*/Map* calls. Reusable or
// developer-only startup logic does NOT belong here — put it in the owning package as an extension
// (the console's server lives in NCode.Identity.ManagementConsole). If a step needs an `if`, a `foreach`,
// a policy builder, or config parsing, move it to a package extension. See csharp-production.instructions.md §4.
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger();

var managementConsole = builder.Services.AddManagementConsole(builder.Configuration);
managementConsole.IdentityBffBuilder.SharedStateLibraryBuilder.AddValkey(builder.Configuration);

// Trust X-Forwarded-* headers only from the proxies in the 'ForwardedHeaders' configuration section.
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);

// Send Strict-Transport-Security as configured by the 'StrictTransportSecurity' configuration section.
builder.Services.AddStrictTransportSecurity(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

// The first two middleware: everything downstream that reads the scheme or host observes the original request rather
// than the proxy-to-application hop.
app.UseTrustedForwardedHeaders();
app.UseStrictTransportSecurity();
app.UseHttpsRedirection();
app.UseBffSecurityHeaders();
app.UseBff();
app.UseAuthentication();
app.UseAuthorization();

app.MapManagementConsole();

// Liveness runs no checks, so a dependency outage never restarts a replica.
// Readiness runs the checks tagged ready.
app.MapHealthProbes();

await app.RunAsync();
