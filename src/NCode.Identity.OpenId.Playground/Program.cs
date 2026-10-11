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

using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;
using NCode.Identity.Server;
using NCode.Identity.Server.InteractiveAuthentication;
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

serviceCollection.AddAntiforgery();

serviceCollection.AddHealthChecks();
serviceCollection.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.All;
});

// DEVELOPMENT CONVENIENCE: let the local SPA dev server (Vite) call discovery and the management API
// cross-origin. Origins come from 'Cors:AllowedOrigins'; Development defaults to the Vite ports.
serviceCollection.AddDeveloperCors(configuration, hostEnvironment);

var openIdOptionsSectionName = Environment.GetEnvironmentVariable("OpenId_OptionsSectionName");

// TODO
serviceCollection.AddIdentityServer().AddConfiguration(configuration, openIdOptionsSectionName);
serviceCollection.AddEntityFrameworkCorePersistence<OpenIdDbContext>();

// Opt in to the generic Entity Framework local-account reference store (the server-owned credential, profile, and
// status payload behind self-issued connections). A host that fronts its own user store would register a different
// ILocalAccountStore instead (for example, the ASP.NET Core Identity adapter).
serviceCollection.AddLocalAccountEntityFramework();

// Enable the resource-owner password grant over the configured local-account store. The account stack provides this
// capability handler (dependency inversion); the Authentication package itself does not depend on accounts.
serviceCollection.AddLocalAccountAuthentication();

// Enable the local-account management endpoints (nested under the tenant-plane group). The account stack provides this
// capability (dependency inversion); the Management slice itself does not depend on accounts.
serviceCollection.AddLocalAccountManagement();

// DEVELOPMENT CONVENIENCE: opt into the interactive login/consent surface (a cookie scheme plus the /account/*
// endpoints that serve the apps/auth bundle and verify local-account credentials) so the authorize flow completes
// end-to-end, and seed a public SPA client + administrator user so the management console can sign in.
serviceCollection.AddInteractiveAuthentication(configuration, hostEnvironment);
serviceCollection.AddDeveloperInteractiveSeed(configuration);

// DEVELOPMENT ONLY: choose how signing keys are provided (see ADR-0002). The default is ephemeral,
// in-memory keys (hermetic, ideal for tests). Local running can opt into persistent developer keys —
// seeded through the store and surviving restarts — via 'DeveloperKeys:Mode=PersistentSigningKey' (set
// in launchSettings.json, so test hosts keep the ephemeral default). Production must supply a stable,
// securely-managed signing key instead.
var developerKeyMode = configuration.GetDeveloperKeyMode();
serviceCollection.AddDeveloperKeys(developerKeyMode, hostEnvironment);

serviceCollection.AddDatabaseDeveloperPageExceptionFilter();

// Select the database provider. A configured 'OpenId' connection string uses SQL Server; otherwise the
// Playground uses a local SQLite file when persistent developer keys are enabled (so state survives
// restarts; reset it by deleting the App_Data/openid-dev.db* files and the next Development run rebuilds it
// from migrations), or a zero-setup in-memory database.
var connectionString = configuration.GetConnectionString("OpenId");
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;
serviceCollection.AddDbContextFactory<OpenIdDbContext>(dbOptions =>
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        dbOptions.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
    }
    else if (developerKeyMode is DeveloperKeyMode.PersistentSigningKey)
    {
        var dataDirectory = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        var databasePath = Path.Combine(dataDirectory, "openid-dev.db");
        dbOptions.UseSqlite(
            $"Data Source={databasePath}",
            sql => sql.MigrationsAssembly(migrationsAssembly)
        );
    }
    else
    {
        dbOptions.UseInMemoryDatabase("OpenId");
    }
});

await using var app = builder.Build();

await InitializeDatabaseAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Turnkey OpenAPI document + Scalar API reference, owned by the composition layer (prod and dev).
app.MapIdentityServerApiReference();

app.UseHttpLogging();
app.UseHttpsRedirection();

// Serve the interactive login/consent bundle's static assets under /account/* (a no-op unless a bundle is configured).
app.UseInteractiveAuthentication();

app.UseRouting();

// Must run after UseRouting and before the authN/authZ + endpoint middleware. A no-op unless
// AddDeveloperCors resolved origins (Development default, or explicit 'Cors:AllowedOrigins').
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
app.MapInteractiveAuthentication();
app.MapHealthChecks("/health").WithName("health_endpoint");

await app.RunAsync();

return;

static async Task InitializeDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var serviceProvider = scope.ServiceProvider;

    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var context = serviceProvider.GetRequiredService<OpenIdDbContext>();

    // The in-memory provider has no migrations; just materialize the model from scratch.
    if (!context.Database.IsRelational())
    {
        await context.Database.EnsureCreatedAsync();
        return;
    }

    // Production applies schema changes deliberately (EF CLI / idempotent SQL script), never on startup. Only
    // Development auto-applies pending migrations so the local developer database evolves without losing data.
    if (!environment.IsDevelopment())
    {
        return;
    }

    // A database with tables but no migrations history was built by EnsureCreated (or by hand) and cannot be
    // migrated in place; fail with guidance instead of a cryptic "table already exists". Never auto-delete.
    var databaseCreator = context.GetService<IRelationalDatabaseCreator>();
    if (
        await databaseCreator.ExistsAsync()
        && await databaseCreator.HasTablesAsync()
        && !(await context.Database.GetAppliedMigrationsAsync()).Any()
    )
    {
        throw new InvalidOperationException(
            "The developer database is not managed by EF Core migrations (it was likely created by an earlier "
                + "EnsureCreated build). Delete 'App_Data/openid-dev.db*' and restart to rebuild it from migrations."
        );
    }

    await context.Database.MigrateAsync();
}
