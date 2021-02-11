using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Playground.Results
{
    // https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/BadRequestResult.cs
    // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/BadRequestResult.cs
    // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/TokenErrorResult.cs

    /// <summary>
    /// A <see cref="ObjectResult{T}"/> that when executed will produce a Bad
    /// Request (400) response and the error details formatted as JSON.
    /// </summary>
    public class BadRequestResult : ObjectResult<IErrorDetails>
    {
        private const int DefaultStatusCode = StatusCodes.Status400BadRequest;

        /// <summary>
        /// Creates a new <see cref="BadRequestResult"/> instance.
        /// </summary>
        public BadRequestResult(string errorCode, string? errorDescription = null)
            : this(new ErrorDetails { Code = errorCode, Description = errorDescription })
        {
            // nothing
        }

        /// <summary>
        /// Creates a new <see cref="BadRequestResult"/> instance.
        /// </summary>
        public BadRequestResult(IErrorDetails errorDetails)
            : base(errorDetails)
        {
            StatusCode = DefaultStatusCode;
        }

        /// <inheritdoc />
        public override Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            // TODO
            // httpContext.Response.SetNoCache();

            return base.ExecuteAsync(httpContext);
        }
    }
}
