using Microsoft.OpenApi.Models;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization;
using NIdentity.OpenId.Endpoints.Discovery;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Playground.Extensions;
using NIdentity.OpenId.Playground.Results;
using NIdentity.OpenId.Playground.Stores;
using NIdentity.OpenId.Results;
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

        services.AddTransient<IHttpResultFactory, HttpResultFactory>();
        services.AddTransient<IExceptionService, ExceptionService>();
        services.AddTransient<ISecretService, SecretService>();
        services.AddTransient<IJwtDecoder, JwtSecurityTokenDecoder>();
        services.AddTransient<IClientStore, NullClientStore>();

        services.AddSingleton<IOpenIdEndpointFactory, OpenIdEndpointFactory>();
        services.AddSingleton<IOpenIdEndpointCollectionProvider, OpenIdEndpointCollectionProvider>();

        services.AddAuthorizationEndpoint();
        services.AddOpenIdEndpoint<DiscoveryEndpointProvider, DiscoveryEndpointHandler>();

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

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapOpenId();
            endpoints.MapControllers();
        });
    }
}
