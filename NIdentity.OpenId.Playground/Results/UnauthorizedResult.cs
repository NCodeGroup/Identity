using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Playground.Results
{
    // https://tools.ietf.org/html/rfc7235#section-3.1
    // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Endpoints/Results/ProtectedResourceErrorResult.cs

    /// <summary>
    /// An <see cref="StatusCodeResult"/> that when executed will produce an empty
    /// <see cref="Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized"/> response.
    /// </summary>
    public class UnauthorizedResult : StatusCodeResult
    {
        private const int DefaultStatusCode = StatusCodes.Status401Unauthorized;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnauthorizedResult"/> class.
        /// </summary>
        public UnauthorizedResult(string challenge, string realm)
            : base(DefaultStatusCode)
        {
            Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
            Realm = realm ?? throw new ArgumentNullException(nameof(realm));
        }

        /// <summary>
        /// Gets the challenge.
        /// </summary>
        public string Challenge { get; }

        /// <summary>
        /// Gets the realm.
        /// </summary>
        public string Realm { get; }

        /// <summary>
        /// Gets or sets the error details.
        /// </summary>
        public IErrorDetails? ErrorDetails { get; set; }

        /// <inheritdoc />
        public override Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            // TODO: escape double quotes

            var values = new List<string>
            {
                Challenge,
                $"realm=\"{Realm}\""
            };

            var error = ErrorDetails?.Code;
            if (!string.IsNullOrEmpty(error))
            {
                values.Add($"error=\"{error}\"");

                var errorDescription = ErrorDetails?.Description;
                if (!string.IsNullOrEmpty(errorDescription))
                    values.Add($"error_description=\"{errorDescription}\"");
            }

            // TODO: add logging

            httpContext.Response.StatusCode = StatusCode;
            httpContext.Response.Headers.Add(HeaderNames.WWWAuthenticate, new StringValues(values.ToArray()));

            return Task.CompletedTask;
        }
    }
}
