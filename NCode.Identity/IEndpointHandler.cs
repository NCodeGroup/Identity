using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Results;

namespace NCode.Identity
{
	public interface IEndpointHandler
	{
		HttpContext HttpContext { get; }

		ValueTask<IHttpResult> HandleAsync(CancellationToken cancellationToken);
	}
}
