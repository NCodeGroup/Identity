#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Contains the result from authenticating an OpenID client.
/// </summary>
public readonly struct ClientAuthenticationResult
{
    /// <summary>
    /// Gets a <see cref="ClientAuthenticationResult"/> singleton instance indicating that the client authentication returned no information.
    /// </summary>
    public static ClientAuthenticationResult Undefined => new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientAuthenticationResult"/> class
    /// indicating that the client authentication returned no information.
    /// </summary>
    public ClientAuthenticationResult()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientAuthenticationResult"/> class
    /// indicating that the client authentication failed.
    /// </summary>
    /// <param name="error">The <see cref="IOpenIdError"/> instance containing the client authentication failure.</param>
    public ClientAuthenticationResult(IOpenIdError error) =>
        Error = error;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientAuthenticationResult"/> class
    /// containing either a public or confidential client.
    /// </summary>
    /// <param name="client">The <see cref="OpenIdClient"/> instance containing either a public or confidential client.</param>
    public ClientAuthenticationResult(OpenIdClient client)
    {
        if (client.IsConfidential)
        {
            ConfidentialClient = client.ConfidentialClient;
        }
        else
        {
            PublicClient = client;
        }
    }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating that the client authentication returned no information.
    /// </summary>
    public bool IsUndefined => !IsError && !IsPublic && !IsConfidential;

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating that the client authentication failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsError => Error != null;

    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> instance containing the client authentication failure.
    /// </summary>
    public IOpenIdError? Error { get; }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating the result contains either a public client or a confidential client.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Client))]
    public bool HasClient => IsConfidential || IsPublic;

    /// <summary>
    /// Gets the <see cref="OpenIdClient"/> instance containing either a public client or a confidential client.
    /// </summary>
    public OpenIdClient? Client => ConfidentialClient ?? PublicClient;

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating the result contains a public client.
    /// </summary>
    [MemberNotNullWhen(true, nameof(PublicClient))]
    public bool IsPublic => PublicClient is { IsConfidential: false };

    /// <summary>
    /// Gets the <see cref="OpenIdClient"/> instance containing the public client.
    /// </summary>
    public OpenIdClient? PublicClient { get; }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating the result contains a confidential client.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ConfidentialClient))]
    public bool IsConfidential => ConfidentialClient is { IsConfidential: true };

    /// <summary>
    /// Gets the <see cref="OpenIdConfidentialClient"/> instance containing the confidential client.
    /// </summary>
    public OpenIdConfidentialClient? ConfidentialClient { get; }
}
