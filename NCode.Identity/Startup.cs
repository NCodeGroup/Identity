using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Identity.Flows.Authorization;

namespace NCode.Identity
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseHttpsRedirection();

			app.UseRouting();

			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				var authMethods = new[] { HttpMethods.Get, HttpMethods.Post };

				//endpoints.MapMethods<AuthorizeHandler>("/{tenantId:guid}/authorize", authMethods,
				//	(handler, httpContext, cancellationToken) => handler.AuthorizeAsync(httpContext, cancellationToken));

				endpoints.MapMethods<AuthorizationHandler>("/{tenantId:guid}/authorize", authMethods);

				endpoints.MapControllers();
			});
		}
	}
}
