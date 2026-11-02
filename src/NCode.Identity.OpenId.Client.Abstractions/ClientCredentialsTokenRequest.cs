// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents a client credentials grant request.
/// </summary>
[PublicAPI]
public sealed class ClientCredentialsTokenRequest : TokenRequest
{
    /// <inheritdoc />
    public override string GrantType => OpenIdProtocol.GrantTypes.ClientCredentials;

    /// <inheritdoc />
    public override void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
    }
}
