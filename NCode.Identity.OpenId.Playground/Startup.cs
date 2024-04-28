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
using Microsoft.OpenApi.Models;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Endpoints.Continue.Logic;
using NCode.Identity.OpenId.Endpoints.Token;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Playground.DataLayer;
using NCode.Identity.OpenId.Playground.Stores;
using NCode.Identity.OpenId.Registration;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NCode.Identity.OpenId.Playground;

internal class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        const int generatorId = 1;
        services.AddIdGen(generatorId);

        services.AddHttpClient();
        services.AddJsonWebTokenService();

        services.AddHealthChecks();
        services.AddHttpLogging(options => { options.LoggingFields = HttpLoggingFields.All; });

        services.AddSingleton<ISystemClock, DefaultSystemClock>();
        services.AddSingleton<ISecretSerializer, DefaultSecretSerializer>();
        services.AddSingleton<ICryptoService, DefaultCryptoService>();

        services.AddSingleton<IPersistedGrantService, DefaultPersistedGrantService>();

        services.AddSingleton<IStoreManagerFactory, EntityStoreManagerFactory<IdentityDbContext>>();
        services.AddSingleton<Func<IIdentityDbContext, IClientStore>>(
            serviceProvider => dbContext =>
                ActivatorUtilities.CreateInstance<ClientStore>(
                    serviceProvider,
                    dbContext));

        services.AddCoreMediatorServices();
        services.AddCoreTenantServices();
        services.AddCoreEndpointServices();

        services.AddSingleton<OpenIdServer, DefaultOpenIdServer>();
        services.AddSingleton<ISettingDescriptorCollectionProvider, SettingDescriptorCollectionProvider>();

        services.AddSingleton<IContinueService, DefaultContinueService>();
        services.AddSingleton<IContinueProviderSelector, DefaultContinueProviderSelector>();
        services.AddSingleton<IOpenIdEndpointProvider, DefaultContinueEndpointHandler>();

        services.AddSingleton<IOpenIdEndpointProvider, DefaultTokenEndpointHandler>();

        services.AddAuthorizationEndpoint();
        services.AddDiscoveryEndpoint();

        services.Configure<OpenIdServerOptions>(Configuration.GetSection("server"));

        services.AddControllers();
        services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "NCode.Identity.OpenId.Playground", Version = "v1" }); });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
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
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health").WithName("health_endpoint");
        });
    }
}