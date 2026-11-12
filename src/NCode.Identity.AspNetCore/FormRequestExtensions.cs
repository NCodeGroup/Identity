// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Provides the extension method that reads a request's form body without turning a malformed body into a server
/// error.
/// </summary>
// One contract for every package that reads a form post, the OpenID endpoints and the BFF alike. See ADR-1500.
[PublicAPI]
public static class FormRequestExtensions
{
    /// <param name="request">The <see cref="HttpRequest"/> instance.</param>
    extension(HttpRequest request)
    {
        /// <summary>
        /// Reads the form body, or returns <c>null</c> when the request carries no form or its body is malformed or over
        /// the form limits — the client's error, which an endpoint answers with <c>400</c>. A parsed form is cached on
        /// the request.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <returns>The form, or <c>null</c> when the request carries no readable form.</returns>
        public async ValueTask<IFormCollection?> TryReadFormAsync(
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!request.HasFormContentType)
            {
                return null;
            }

            // A malformed body, or one over the form limits, throws InvalidDataException: the request is bad, not the
            // server.
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
