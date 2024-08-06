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

using System.Text.Json;
using IdGen.DependencyInjection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NCode.Identity.DataProtection;
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Endpoints.Authorization;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Endpoints.Discovery;
using NCode.Identity.OpenId.Endpoints.Token;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;

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

        services.AddHealthChecks();
        services.AddHttpLogging(options => { options.LoggingFields = HttpLoggingFields.All; });

        services.AddHttpClient();

        services.Configure<OpenIdServerOptions>(Configuration.GetSection("server"));

        services.AddSecureDataProtectionServices();
        services.AddSecretServices();
        services.AddJoseServices();
        services.AddJsonWebTokenServices();

        services.AddOpenIdServices();

        services.AddMediatorServices();
        services.AddTenantServices();
        services.AddEndpointServices();
        services.AddClientServices();
        services.AddTokenServices();

        services.AddContinueEndpoint();
        services.AddDiscoveryEndpoint();
        services.AddAuthorizationEndpoint();
        services.AddTokenEndpoint();

        services.AddSecretPersistenceServices();
        services.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();

        services.AddDbContextFactory<OpenIdDbContext>(builder =>
        {
            // TODO
            builder.UseInMemoryDatabase("OpenId");
        });

        services.AddControllers();
        services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "NCode.Identity.OpenId.Playground", Version = "v1" }); });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        using (var serviceScope = app.ApplicationServices.CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            BootstrapData(context);
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NCode.Identity.OpenId.Playground v1"));
        }

        app.UseHttpLogging();
        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<OpenIdMiddleware>();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapOpenId().WithHttpLogging(HttpLoggingFields.All);
            endpoints.MapControllers().WithHttpLogging(HttpLoggingFields.All);
            endpoints.MapHealthChecks("/health").WithName("health_endpoint");
        });
    }

    private static void BootstrapData(OpenIdDbContext context)
    {
        const string tenantId = StaticSingleOpenIdTenantOptions.DefaultTenantId;
        var tenant = context.Tenants.FirstOrDefault(tenant => tenant.TenantId == tenantId);
        if (tenant == null)
        {
            tenant = new TenantEntity
            {
                TenantId = tenantId,
                NormalizedTenantId = tenantId.ToUpperInvariant(),
                DisplayName = StaticSingleOpenIdTenantOptions.DefaultDisplayName,
                DomainName = null,
                NormalizedDomainName = null,
                ConcurrencyToken = Guid.NewGuid().ToString(),
                IsDisabled = false,
                Settings = JsonSerializer.SerializeToElement(null, typeof(object)),
                Secrets = Array.Empty<TenantSecretEntity>(),
            };

            context.Tenants.Add(tenant);
        }

        context.SaveChanges();
    }
}
