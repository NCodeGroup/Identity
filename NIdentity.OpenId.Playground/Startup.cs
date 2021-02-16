using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NIdentity.OpenId.Handlers;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Playground.Handlers;
using NIdentity.OpenId.Playground.Results;
using NIdentity.OpenId.Playground.Stores;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NIdentity.OpenId.Playground
{
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

            services.AddTransient<IHttpEndpointHandler<ProcessTestEndpoint>, ProcessTestEndpointHandler>();
            services.AddTransient<IHttpEndpointHandler<ProcessAuthorizationEndpoint>, ProcessAuthorizationEndpointHandler>();

            services.AddTransient<IRequestResponseHandler<GetAuthorizationRequest, IAuthorizationRequest>, GetAuthorizationRequestHandler>();
            services.AddTransient<IRequestHandler<ValidateAuthorizationRequest>, ValidateAuthorizationRequestHandler>();

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "NIdentity.OpenId.Playground", Version = "v1" });
            });
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
                endpoints.MapGet("/test", httpContext => new ProcessTestEndpoint(httpContext));
                endpoints.MapMethods("/authorize", HttpVerbs.GetAndPost, httpContext => new ProcessAuthorizationEndpoint(httpContext));
                endpoints.MapControllers();
            });
        }
    }
}
