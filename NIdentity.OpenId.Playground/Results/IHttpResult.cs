using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Playground.Results
{
	// https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Abstractions/src/IActionResult.cs

	/// <summary>
	/// Defines a contract that represents the result of an OpenID endpoint.
	/// </summary>
	public interface IHttpResult
	{
		/// <summary>
		/// Executes the result operation of the endpoint asynchronously. This method is called by OpenID to process
		/// the result of an endpoint.
		/// </summary>
		/// <param name="httpContext">The context in which the result is executed. The context information includes
		/// information about the endpoint that was executed and request information.</param>
		/// <returns>A task that represents the asynchronous execute operation.</returns>
		Task ExecuteAsync(HttpContext httpContext);
	}
}
