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
using Microsoft.OpenApi;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.Server;
using NCode.Registration.AspNetCore;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NCode.Identity.OpenId.Playground;

internal class Startup(IConfiguration configuration, IWebHostEnvironment hostEnvironment)
{
    private IConfiguration Configuration { get; } = configuration;
    private IWebHostEnvironment HostEnvironment { get; } = hostEnvironment;

    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddAntiforgery();

        serviceCollection.AddHealthChecks();
        serviceCollection.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.All;
        });

        var openIdOptionsSectionName = Environment.GetEnvironmentVariable(
            "OpenId_OptionsSectionName"
        );

        serviceCollection.AddEndpointsApiExplorer();

        // TODO
        serviceCollection
            .AddIdentityServer()
            .AddConfiguration(Configuration, openIdOptionsSectionName);
        serviceCollection.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();

        // DEVELOPMENT ONLY: choose how signing keys are provided (see ADR-0002). The default is ephemeral,
        // in-memory keys (hermetic, ideal for tests). Local running can opt into persistent developer keys —
        // seeded through the store and surviving restarts — via 'DeveloperKeys:Mode=PersistentSigningKey' (set
        // in launchSettings.json, so test hosts keep the ephemeral default). Production must supply a stable,
        // securely-managed signing key instead.
        var developerKeyMode = Configuration.GetDeveloperKeyMode();
        serviceCollection.AddDeveloperKeys(developerKeyMode, HostEnvironment);

        serviceCollection.AddDatabaseDeveloperPageExceptionFilter();

        // Select the database provider. A configured 'OpenId' connection string uses SQL Server; otherwise the
        // Playground uses a local SQLite file when persistent developer keys are enabled (so state survives
        // restarts; reset it by deleting the App_Data/openid-dev.db* files and the next Development run rebuilds it
        // from migrations), or a zero-setup in-memory database.
        var connectionString = Configuration.GetConnectionString("OpenId");
        var migrationsAssembly = typeof(Startup).Assembly.GetName().Name;
        serviceCollection.AddDbContextFactory<OpenIdDbContext>(builder =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                builder.UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly)
                );
            }
            else if (developerKeyMode is DeveloperKeyMode.PersistentSigningKey)
            {
                var databasePath = Path.Combine(
                    HostEnvironment.ContentRootPath,
                    "App_Data",
                    "openid-dev.db"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
                builder.UseSqlite(
                    $"Data Source={databasePath}",
                    sql => sql.MigrationsAssembly(migrationsAssembly)
                );
            }
            else
            {
                builder.UseInMemoryDatabase("OpenId");
            }
        });

        serviceCollection.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(
                "v1",
                new OpenApiInfo { Title = "NCode.Identity.OpenId.Playground", Version = "v1" }
            );
        });
    }

    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger(c => { });
            app.UseSwaggerUI(c =>
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "NCode.Identity.OpenId.Playground v1")
            );
        }

        app.UseHttpLogging();
        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapEndpointGroups();
            endpoints.MapHealthChecks("/health").WithName("health_endpoint");
        });
    }
}
