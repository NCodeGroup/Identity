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

using Microsoft.OpenApi.Models;
using NCode.Identity.JsonWebTokens;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization;
using NIdentity.OpenId.Endpoints.Discovery;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Playground.Stores;
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
        services.AddHttpClient();

        services.AddJsonWebTokenService();

        services.AddTransient<ISecretSerializer, SecretSerializer>();
        services.AddTransient<IClientStore, NullClientStore>();

        services.AddScoped<IMediator, MediatorImpl>();
        services.AddSingleton<IOpenIdEndpointFactory, OpenIdEndpointFactory>();
        services.AddSingleton<IOpenIdEndpointCollectionProvider, OpenIdEndpointCollectionProvider>();

        services.AddAuthorizationEndpoint();
        services.AddOpenIdEndpoint<DiscoveryEndpointProvider, DiscoveryEndpointHandler, DiscoveryEndpointCommand>();

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
