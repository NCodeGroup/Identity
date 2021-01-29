using GreenPipes;
using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Playground.Contexts
{
    internal interface IReceiveContext : PipeContext
    {
        HttpContext HttpContext { get; }
    }

    internal class ReceiveContext : BasePipeContext, IReceiveContext
    {
        public HttpContext HttpContext { get; }

        public ReceiveContext(HttpContext httpContext)
            : base(httpContext.RequestAborted)
        {
            HttpContext = httpContext;
        }
    }
}
