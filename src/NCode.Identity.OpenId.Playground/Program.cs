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
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.Management;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;
using NCode.Identity.OpenId.Playground.Database;
using NCode.Identity.Server;
using NCode.Registration;
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
var identityServer = serviceCollection.AddIdentityServer();
identityServer.AddConfiguration(configuration, openIdOptionsSectionName);

// Bind the frontend runtime bootstrap document (ADR-0064) from the 'Frontend' configuration section so the host can
// publish this deployment's coordinates (API base, issuer, client id, feature flags, branding) to the tier-agnostic
// bundles via MapFrontendBootstrap below.
identityServer.AddFrontendBootstrap(configuration);

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

// DEVELOPMENT CONVENIENCE: seed a public SPA client + administrator user so the management console can sign in.
identityServer.AddDeveloperInteractiveSeed(configuration);

// DEVELOPMENT ONLY: choose how signing keys are provided (see ADR-0002). The default is ephemeral,
// in-memory keys (hermetic, ideal for tests). Local running can opt into persistent developer keys —
// seeded through the store and surviving restarts — via 'DeveloperKeys:Mode=PersistentSigningKey' (set
// in launchSettings.json, so test hosts keep the ephemeral default). Production must supply a stable,
// securely-managed signing key instead.
var developerKeyMode = configuration.GetDeveloperKeyMode();
identityServer.AddDeveloperKeys(developerKeyMode);

serviceCollection.AddDatabaseDeveloperPageExceptionFilter();

// The dev database is Postgres, orchestrated by the Aspire AppHost (ADR-0073): the AppHost provisions the container
// and injects the 'openiddb' connection string, and a dedicated migration worker applies the schema before this host
// starts — so the api never bootstraps the database itself. The developer data-protection key ring is persisted in the
// same database, so one purge resets the OpenID data, the seeded signing key, and the ring that protects it together.
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
        .SetApplicationName("NCode.Identity.OpenId.Playground");
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
