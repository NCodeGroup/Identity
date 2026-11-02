// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the extension method that reads a request's form body without turning a malformed body into a server
/// error.
/// </summary>
[PublicAPI]
public static class FormRequestExtensions
{
    /// <param name="request">The <see cref="HttpRequest"/> instance.</param>
    extension(HttpRequest request)
    {
        /// <summary>
        /// Reads the form body, or returns <c>null</c> when the request carries no form or its body is malformed or over
        /// the form limits — the client's error, which an endpoint answers with <c>400 invalid_request</c>. A parsed
        /// form is cached on the request.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        public async ValueTask<IFormCollection?> TryReadFormAsync(
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!request.HasFormContentType)
            {
                return null;
            }

            try
            {
                return await request.ReadFormAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }
}
