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

using System.Net;
using System.Net.Http.Headers;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticator"/> for the <c>client_secret_basic</c> method, which sends
/// the client's credentials in the HTTP <c>Authorization</c> header.
/// </summary>
internal class ClientSecretBasicClientAuthenticator(IClientKeyRingProvider keyRingProvider)
    : IClientAuthenticator
{
    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;

    /// <inheritdoc />
    public string MethodName => ClientAuthenticationMethods.ClientSecretBasic;

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var registration = context.Registration;
        var secret = SharedSecretReader.ReadSecret(KeyRingProvider, registration);
        var credentials =
            $"{WebUtility.UrlEncode(registration.ClientId)}:{WebUtility.UrlEncode(secret)}";

        context.Request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(SecureEncoding.UTF8.GetBytes(credentials))
        );
        return ValueTask.CompletedTask;
    }
}
