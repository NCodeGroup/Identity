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
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Playground;
using NCode.Identity.Server;
using NCode.Registration.AspNetCore;
using Scalar.AspNetCore;

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

var openIdOptionsSectionName = Environment.GetEnvironmentVariable("OpenId_OptionsSectionName");

// TODO
serviceCollection.AddIdentityServer().AddConfiguration(configuration, openIdOptionsSectionName);
serviceCollection.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();

// Development-only: pre-fill the bootstrap admin client_credentials example on the token endpoint in Scalar.
serviceCollection.AddOpenApi(options =>
    options.AddDocumentTransformer<BootstrapTokenExampleDocumentTransformer>()
);

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
    app.MapOpenApi();
    // "Bearer" matches the scheme name declared by SecuritySchemeDocumentTransformer in NCode.Identity.Server.
    app.MapScalarApiReference(options => options.AddPreferredSecuritySchemes("Bearer"));
}

app.UseHttpLogging();
app.UseHttpsRedirection();

app.UseRouting();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpointGroups();
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
