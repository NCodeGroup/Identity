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

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides a base class for the RFC 8705 mutual-TLS <see cref="IClientAuthenticator"/> methods, which authenticate the
/// client with its TLS client certificate and send only the <c>client_id</c> form parameter.
/// </summary>
internal abstract class MutualTlsClientAuthenticator : IClientAuthenticator
{
    /// <inheritdoc />
    public abstract string MethodName { get; }

    /// <inheritdoc />
    public bool UsesMutualTls => true;

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.ClientId,
                context.Registration.ClientId
            )
        );
        return ValueTask.CompletedTask;
    }
}
