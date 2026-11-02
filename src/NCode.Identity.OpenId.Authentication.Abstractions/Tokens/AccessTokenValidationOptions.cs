// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Tokens;

/// <summary>
/// Contains what a reader of an access token adds to the policy <see cref="IIssuedTokenValidator"/> validates the token
/// under: the audiences the reader requires, and the keys of the reader that may decrypt the token besides the tenant's.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AccessTokenValidationOptions
{
    /// <summary>
    /// Gets the audiences the access token must name in its <c>aud</c> claim; the token is valid when it names any of
    /// them. When empty, the audience is not checked.
    /// </summary>
    public IReadOnlyCollection<string> RequiredAudiences { get; init; } = [];

    /// <summary>
    /// Gets the keys, besides the tenant's, that may decrypt an encrypted access token — the keys of a resource server
    /// that decrypts the tokens issued for it, when that resource server is the reader.
    /// </summary>
    public IReadOnlyCollection<SecretKey> AdditionalDecryptionKeys { get; init; } = [];
}
