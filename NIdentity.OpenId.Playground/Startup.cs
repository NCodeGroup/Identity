using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NIdentity.OpenId.Handlers;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Playground.Handlers;
using NIdentity.OpenId.Playground.Logic;
using NIdentity.OpenId.Playground.Results;
using NIdentity.OpenId.Playground.Stores;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Validation;

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

            app.UseExceptionHandler(cfg =>
            {
                cfg.Use(next =>
                {
                    return async httpContext =>
                    {
                        var exceptionHandlerFeature = httpContext.Features.Get<IExceptionHandlerFeature>();
                        if (exceptionHandlerFeature.Error is OpenIdException exception)
                        {
                            var statusCode = exception.StatusCode ?? StatusCodes.Status500InternalServerError;
                            var errorDetails = new ErrorDetails
                            {
                                StatusCode = statusCode,
                                Uri = exception.ErrorUri,
                                Code = exception.ErrorCode,
                                Description = exception.ErrorDescription,
                                ExtensionData = exception.ExtensionData
                            };
                            var httpResult = HttpResultFactory.Object(statusCode, errorDetails);
                            await httpResult.ExecuteAsync(httpContext);
                            await httpContext.Response.CompleteAsync();
                        }
                        else
                        {
                            ExceptionDispatchInfo.Throw(exceptionHandlerFeature.Error);
                        }

                        await next(httpContext);
                    };
                });
            });

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test", async httpContext =>
                {
                    var request = new ProcessTestEndpoint(httpContext);
                    var handler = httpContext.RequestServices.GetRequiredService<IHttpEndpointHandler<ProcessTestEndpoint>>();
                    var httpResult = await handler.HandleAsync(request, httpContext.RequestAborted);
                    await httpResult.ExecuteAsync(httpContext);
                });

                endpoints.MapGet("/authorize", async httpContext =>
                {
                    var request = new ProcessAuthorizationEndpoint(httpContext);
                    var handler = httpContext.RequestServices.GetRequiredService<IHttpEndpointHandler<ProcessAuthorizationEndpoint>>();
                    var httpResult = await handler.HandleAsync(request, httpContext.RequestAborted);
                    await httpResult.ExecuteAsync(httpContext);
                });

                //endpoints.MapGet("/test", httpContext => httpContextPipe.Send(httpContext));
                //endpoints.MapGet("/test2", httpContext => receivePipe.Send(new ReceiveContext(httpContext)));
                endpoints.MapControllers();
            });
        }
    }
}
