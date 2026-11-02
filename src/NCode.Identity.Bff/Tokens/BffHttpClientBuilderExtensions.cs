// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides extension methods for <see cref="IHttpClientBuilder"/> to attach the session's tokens to outbound calls.
/// </summary>
[PublicAPI]
public static class BffHttpClientBuilderExtensions
{
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Adds a handler that attaches the token of the requirement named by <paramref name="request"/>, and its DPoP proof
        /// when the token is DPoP-bound, to every outbound request, and retries a resource server's DPoP nonce challenge
        /// once. A failure to obtain the token surfaces as a <see cref="BffTokenUnavailableException"/>, which
        /// <c>UseBff()</c> answers.
        /// </summary>
        /// <param name="request">The <see cref="BffTokenRequest"/> that names the token requirement.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> so that additional calls can be chained.</returns>
        public IHttpClientBuilder AddBffAccessTokenHandler(BffTokenRequest request)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(request);

            return builder.AddHttpMessageHandler(sp =>
                ActivatorUtilities.CreateInstance<BffAccessTokenHandler>(sp, request)
            );
        }
    }
}
