using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Results;

/// <summary>
/// Defines a contract that represents the result of a HTTP operation.
/// </summary>
public interface IHttpResult
{
    /// <summary>
    /// Executes the result operation of the HTTP operation asynchronously. This method is called by the framework
    /// to process the result of a HTTP operation.
    /// </summary>
    /// <param name="httpContext">The context in which the result is executed. The context information includes
    /// information about the HTTP operation that was executed and request information.</param>
    /// <returns>A task that represents the asynchronous execute operation.</returns>
    ValueTask ExecuteAsync(HttpContext httpContext);
}
