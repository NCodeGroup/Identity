using System;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using GreenPipes;
using GreenPipes.Configurators;
using GreenPipes.Contexts;
using GreenPipes.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Playground.Contexts;
using NIdentity.OpenId.Playground.Pipes;
using NIdentity.OpenId.Playground.Results;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NIdentity.OpenId.Playground
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "NIdentity.OpenId.Playground", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            IPipeRouter pipeRouter = new CompositePipeRouter();

            var otherPipe1 = pipeRouter.CreateRequestPipe<HttpContext, IAuthorizationContext>();

            var otherPipe2 = pipeRouter.CreateRequestPipe<IAuthorizationContext, IHttpResult>();

            var authPipe = Pipe.New<IAuthorizationContext>(configurator =>
            {
                configurator.UseExecute(context =>
                {
                });
            });
            pipeRouter.ConnectPipe(authPipe);

            var requestPipe = Pipe.New<RequestContext>(configurator =>
            {
                configurator.UseRequestHandler<LoadAuthorization, AuthorizationContext>();
            });

            var loadAuthorizationPipe = requestPipe.CreateRequestPipe<LoadAuthorization, IAuthorizationContext>();

            var httpContextRequestPipe = Pipe.New<RequestContext<HttpContext>>(configurator =>
            {
                configurator.UseExecuteAsync(async httpContextRequestContext =>
                {
                    var httpContext = httpContextRequestContext.Request;

                    var authResult =
                        await loadAuthorizationPipe.Send(new LoadAuthorization { HttpContext = httpContext });
                    var auth = authResult.Result;

                    await authPipe.Send(auth);

                    var authorizationResultContext = await otherPipe1.Send(httpContext);
                    var httpResultContext = await otherPipe2.Send(authorizationResultContext.Result);
                    httpContextRequestContext.TrySetResult(httpResultContext.Result);

                    // TODO: create the HTTP result
                    //var httpResult = HttpResultFactory.Ok("hello world");
                    //context.TrySetResult(httpResult);
                });
            });
            pipeRouter.ConnectPipe(httpContextRequestPipe);

            var httpContextPipe = pipeRouter.CreateRequestPipe<HttpContext, IHttpResult>(configurator =>
            {
                configurator.UseExecuteAsync(context => context.Result.ExecuteAsync(context.Request));
            });

            //

            var consumeFilter = new DynamicFilter<IConsumeContext>(new CompositePipeContextConverterFactory());
            var consumePipeInternal = Pipe.New<IConsumeContext>(cfg =>
            {
                cfg.UseFilter(consumeFilter);
            });
            var consumePipe = new ConsumePipe(consumePipeInternal);

            var receivePipeInternal = Pipe.New<IReceiveContext>(cfg =>
            {
                cfg.UseFilter(new DeserializeFilter(consumePipe));
            });
            var receivePipe = new ReceivePipe(receivePipeInternal, consumePipe);

            //

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
                endpoints.MapGet("/test", httpContext => httpContextPipe.Send(httpContext));
                endpoints.MapGet("/test2", httpContext => receivePipe.Send(new ReceiveContext(httpContext)));
                endpoints.MapControllers();
            });
        }
    }

    internal class AuthorizationContextFromHttpPipeContextFactory :
        IPipeContextSource<IAuthorizationContext, IReceiveContext>
    {
        public async Task Send(IReceiveContext context, IPipe<IAuthorizationContext> pipe)
        {
            var request = (IAuthorizationRequest)null!;
            var response = (IAuthorizationResponse)null!;

            var payloads = new object[]
            {
                context.HttpContext,
                context.HttpContext.RequestServices
            };

            var authorizationContext = new AuthorizationContext(context, request, response, payloads);

            await pipe.Send(authorizationContext);
        }

        public void Probe(ProbeContext context)
        {
            throw new System.NotImplementedException();
        }
    }

    public class LoadAuthorization
    {
        public HttpContext HttpContext { get; set; }
    }

    internal interface IRequestHandler<in TRequest, TResponse>
    {
        ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
    }

    internal static class DispatchConfiguratorExtensions
    {
        public static void UseRequestHandler<TRequest, TResponse>(
            this IPipeConfigurator<RequestContext> pipeConfigurator)
            where TRequest : class
            where TResponse : class
        {
            pipeConfigurator.UseDispatch(
                new RequestConverterFactory(),
                dispatchConfigurator =>
                {
                    dispatchConfigurator.Handle<TRequest>(requestConfigurator =>
                    {
                        requestConfigurator.UseContextFilter(ctx => Task.FromResult(!ctx.IsCompleted));

                        requestConfigurator.UseExecuteAsync(async context =>
                        {
                            if (!context.TryGetPayload<IServiceProvider>(out var serviceProvider))
                                throw new InvalidOperationException();

                            var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                            using var serviceScope = serviceScopeFactory.CreateScope();

                            var handler = serviceScope.ServiceProvider
                                .GetRequiredService<IRequestHandler<TRequest, TResponse>>();

                            var response = await handler.HandleAsync(context.Request, context.CancellationToken);

                            context.TrySetResult(response);
                        });
                    });
                });
        }
    }
}
