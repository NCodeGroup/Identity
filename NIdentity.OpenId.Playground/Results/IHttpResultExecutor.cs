using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Playground.Results
{
    // https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/Infrastructure/IActionResultExecutor.cs

    /// <summary>
    /// Defines an interface for a service which can execute a particular kind of <see cref="IHttpResult"/> by
    /// manipulating the <see cref="HttpResponse"/>.
    /// </summary>
    /// <typeparam name="THttpResult">The type of <see cref="IHttpResult"/>.</typeparam>
    /// <remarks>
    /// Implementations of <see cref="IHttpResultExecutor{TResult}"/> are typically called by the
    /// <see cref="IHttpResult.ExecuteAsync(HttpContext)"/> method of the corresponding action result type.
    /// Implementations should be registered as singleton services.
    /// </remarks>
    public interface IHttpResultExecutor<in THttpResult>
        where THttpResult : notnull, IHttpResult
    {
        /// <summary>
        /// Asynchronously executes the action result, by modifying the <see cref="HttpResponse"/>.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request."/></param>
        /// <param name="result">The action result to execute.</param>
        /// <returns>A <see cref="Task"/> which represents the asynchronous operation.</returns>
        Task ExecuteAsync(HttpContext httpContext, THttpResult result);
    }
}
