using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Playground.Extensions;

namespace NIdentity.OpenId.Playground.Results;
// https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/BadRequestResult.cs
// https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/BadRequestResult.cs
// https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/TokenErrorResult.cs

/// <summary>
/// A <see cref="ObjectResult{T}"/> that when executed will produce a Not Found (404) response and the error
/// details formatted as JSON.
/// </summary>
public class NotFoundObjectResult<T> : ObjectResult<T>
{
    private const int DefaultStatusCode = StatusCodes.Status404NotFound;

    /// <summary>
    /// Creates a new <see cref="NotFoundObjectResult{T}"/> with the given <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to format as JSON.</param>
    public NotFoundObjectResult(T value)
        : base(value)
    {
        StatusCode = DefaultStatusCode;
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(HttpContext httpContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        httpContext.Response.SetNoCache();

        return base.ExecuteAsync(httpContext);
    }
}