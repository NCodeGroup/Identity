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

using IdGen.DependencyInjection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Playground.DevelopmentEnvironment;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server;

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

    public void ConfigureServices(IServiceCollection services)
    {
        const int generatorId = 1;
        services.AddIdGen(generatorId);

        services.AddRouting();
        services.AddAntiforgery();

        services.AddHealthChecks();
        services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.All;
        });
        services.AddHttpClient();

        var openIdOptionsSectionName = Environment.GetEnvironmentVariable(
            "OpenId_OptionsSectionName"
        );
        if (string.IsNullOrEmpty(openIdOptionsSectionName))
        {
            openIdOptionsSectionName = OpenIdOptions.DefaultSectionName;
        }

        services.Configure<OpenIdOptions>(Configuration.GetSection(openIdOptionsSectionName));
        services.Configure<OpenIdOptions>(options =>
            options.SectionName = openIdOptionsSectionName
        );

        // Tenant selection is configured separately from tenant materialization; defaults resolve the
        // single "default" tenant when this section is absent.
        services.Configure<TenantResolutionOptions>(
            Configuration.GetSection($"{openIdOptionsSectionName}:TenantResolution")
        );

        services.AddEndpointsApiExplorer();

        // TODO
        services.AddIdentityServer();
        services.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();

        // DEVELOPMENT ONLY: choose how signing keys are provided (see ADR-0002). The default is ephemeral,
        // in-memory keys (hermetic, ideal for tests). Local running can opt into persistent developer keys —
        // seeded through the store and surviving restarts — via 'DeveloperKeys:Mode=PersistentSigningKey' (set
        // in launchSettings.json, so test hosts keep the ephemeral default). Production must supply a stable,
        // securely-managed signing key instead.
        var usePersistentDeveloperKeys = string.Equals(
            Configuration["DeveloperKeys:Mode"],
            "PersistentSigningKey",
            StringComparison.OrdinalIgnoreCase
        );

        if (usePersistentDeveloperKeys)
        {
            var keyRingDirectory = new DirectoryInfo(
                Path.Combine(HostEnvironment.ContentRootPath, "App_Data", "dp-keys")
            );
            services.AddDeveloperSigningKey(keyRingDirectory);
        }
        else
        {
            services.AddEphemeralDeveloperKeys();
        }

        services.AddDatabaseDeveloperPageExceptionFilter();

        // Select the database provider. A configured 'OpenId' connection string uses SQL Server; otherwise the
        // Playground uses a local SQLite file when persistent developer keys are enabled (so state survives
        // restarts and can be reset by deleting the file), or a zero-setup in-memory database.
        var connectionString = Configuration.GetConnectionString("OpenId");
        services.AddDbContextFactory<OpenIdDbContext>(builder =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                builder.UseSqlServer(connectionString);
            }
            else if (usePersistentDeveloperKeys)
            {
                var databasePath = Path.Combine(
                    HostEnvironment.ContentRootPath,
                    "App_Data",
                    "openid-dev.db"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
                builder.UseSqlite($"Data Source={databasePath}");
            }
            else
            {
                builder.UseInMemoryDatabase("OpenId");
            }
        });

        services.AddControllers();
        services.AddSwaggerGen(c =>
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
            endpoints.MapIdentityEndpoints();
            endpoints.MapControllers().WithHttpLogging(HttpLoggingFields.All);
            endpoints.MapHealthChecks("/health").WithName("health_endpoint");
        });
    }
}
