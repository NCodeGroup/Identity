using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using NCode.Identity.Validation;

namespace NCode.Identity.Results
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
        public BadRequestResult(string error)
            : this(error, null)
        {
            // nothing
        }

        /// <summary>
        /// Creates a new <see cref="BadRequestResult"/> instance.
        /// </summary>
        public BadRequestResult(string error, string errorDescription)
            : this(new ErrorDetails { ErrorCode = error, ErrorDescription = errorDescription })
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

            httpContext.Response.SetNoCache();

            return base.ExecuteAsync(httpContext);
        }
    }
}
