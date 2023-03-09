using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Playground.Results;
// https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/StatusCodeResult.cs

/// <summary>
/// Represents an <see cref="IHttpResult"/> that when executed will
/// produce an HTTP response with the given response status code.
/// </summary>
public class StatusCodeResult : IHttpResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StatusCodeResult"/> class
    /// with the given <paramref name="statusCode"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    public StatusCodeResult(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <inheritdoc />
    public virtual ValueTask ExecuteAsync(HttpContext httpContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        // TODO: add logging

        httpContext.Response.StatusCode = StatusCode;

        return ValueTask.CompletedTask;
    }
}
