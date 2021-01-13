using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace NCode.Identity
{
	public static class EndpointConventionBuilderExtensions
	{
		public static void MapMethods<THandler>(this IEndpointRouteBuilder endpoints, string pattern, IEnumerable<string> methods)
			where THandler : IEndpointHandler
		{
			var factory = ActivatorUtilities.CreateFactory(typeof(THandler), new[] { typeof(HttpContext) });
			endpoints.MapMethods(pattern, methods, async httpContext =>
			{
				var cancellationToken = httpContext.RequestAborted;
				var handler = (THandler)factory(httpContext.RequestServices, new object[] { httpContext });
				try
				{
					// TODO: check for disposable
					var result = await handler.HandleAsync(cancellationToken);
					await result.ExecuteAsync(httpContext);
				}
				finally
				{
					switch (handler)
					{
						case IAsyncDisposable asyncDisposable:
							await asyncDisposable.DisposeAsync();
							break;

						case IDisposable disposable:
							disposable.Dispose();
							break;
					}
				}
			});
		}
	}
}
