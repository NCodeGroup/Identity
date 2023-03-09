using NIdentity.OpenId.Playground.Extensions;

namespace NIdentity.OpenId.Playground.Results;
// https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/BadRequestResult.cs
// https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/BadRequestResult.cs
// https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/TokenErrorResult.cs

/// <summary>
/// A <see cref="ObjectResult{T}"/> that when executed will produce a Bad
/// Request (400) response and the error details formatted as JSON.
/// </summary>
public class BadRequestObjectResult<T> : ObjectResult<T>
{
    private const int DefaultStatusCode = StatusCodes.Status400BadRequest;

    /// <summary>
    /// Creates a new <see cref="BadRequestObjectResult{T}"/> with the given <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to format as JSON.</param>
    public BadRequestObjectResult(T value)
        : base(value)
    {
        StatusCode = DefaultStatusCode;
    }

    /// <inheritdoc />
    public override ValueTask ExecuteAsync(HttpContext httpContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        httpContext.Response.SetNoCache();

        return base.ExecuteAsync(httpContext);
    }
}
