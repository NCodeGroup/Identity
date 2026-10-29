#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
