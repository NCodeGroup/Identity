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
using Microsoft.OpenApi.Models;
using NCode.Identity.JsonWebTokens;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Playground.Stores;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.ServiceCollectionExtensions;
using NIdentity.OpenId.Settings;
using NIdentity.OpenId.Stores;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NIdentity.OpenId.Playground;

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

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISecretSerializer, SecretSerializer>();
        services.AddSingleton<ICryptoService, CryptoService>();

        services.AddSingleton<IClientStore, EmptyClientStore>();
        services.AddSingleton<ITenantStore, EmptyTenantStore>();
        services.AddSingleton<IAuthorizationCodeStore, EmptyAuthorizationCodeStore>();

        services.AddCoreMediatorServices();
        services.AddCoreTenantServices();
        services.AddCoreEndpointServices();

        services.AddSingleton<ISettingDescriptorCollectionProvider, SettingDescriptorCollectionProvider>();
        services.AddSingleton<IOpenIdServerSettingsProvider, OpenIdServerSettingsProvider>();

        services.AddAuthorizationEndpoint();
        services.AddDiscoveryEndpoint();

        services.Configure<OpenIdServerOptions>(Configuration.GetSection("server"));

        services.AddControllers();
        services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "NIdentity.OpenId.Playground", Version = "v1" }); });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NIdentity.OpenId.Playground v1"));
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapOpenId();
            endpoints.MapControllers();
        });
    }
}
