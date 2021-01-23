using GreenPipes;
using Microsoft.AspNetCore.Http;
using System;

namespace NIdentity.OpenId.Playground.Contexts
{
    internal interface IHttpPipeContext : PipeContext
    {
        HttpContext HttpContext { get; }

        IServiceProvider ServiceProvider { get; }
    }

    internal class HttpPipeContext : BasePipeContext, IHttpPipeContext
    {
        public HttpContext HttpContext { get; }

        public IServiceProvider ServiceProvider => HttpContext.RequestServices;

        public HttpPipeContext(HttpContext httpContext)
            : base(httpContext.RequestAborted)
        {
            HttpContext = httpContext;
        }
    }
}
