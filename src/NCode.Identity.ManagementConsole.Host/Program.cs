// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using NCode.Identity;
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

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseBffSecurityHeaders();
app.UseBff();
app.UseAuthentication();
app.UseAuthorization();

app.MapManagementConsole();

// Liveness runs no checks: it reports only that the process answers, so a dependency outage never restarts a replica.
// Readiness runs the checks tagged 'ready' (Valkey when configured) and takes the replica out of rotation while a
// dependency it needs is unreachable.
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
