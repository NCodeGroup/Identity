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
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Playground.DevelopmentEnvironment;
using NCode.Identity.Server;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NCode.Identity.OpenId.Playground;

internal class Startup(IConfiguration configuration)
{
    private IConfiguration Configuration { get; } = configuration;

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

        services.AddEndpointsApiExplorer();

        // TODO
        services.AddIdentityServer();
        services.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();

        // DEVELOPMENT ONLY: generate an ephemeral in-memory RSA signing key so the server is fully
        // runnable (token signing + JWKS) without any configured/persisted secrets. Production must
        // supply a stable, securely-managed signing key instead. See docs/adr/0002.
        services.AddEphemeralDeveloperKeys();

        services.AddDatabaseDeveloperPageExceptionFilter();

        // Select the database provider based on configuration. When a connection string named 'OpenId'
        // is provided (e.g. via appsettings.json, environment variables, or user secrets) SQL Server is
        // used; otherwise the Playground falls back to a zero-setup in-memory database so it can run
        // without any external dependencies.
        var connectionString = Configuration.GetConnectionString("OpenId");
        services.AddDbContextFactory<OpenIdDbContext>(builder =>
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                builder.UseInMemoryDatabase("OpenId");
            }
            else
            {
                builder.UseSqlServer(connectionString);
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
