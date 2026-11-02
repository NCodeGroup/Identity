// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose;

/// <summary>
/// Contains the credentials and options for a single recipient of a JWE token.
/// A JWE token always has at least one recipient; the compact serialization carries exactly one (and folds the
/// recipient's key-management headers into the single protected header), while the JSON serialization may carry
/// several. The content-encryption key (CEK) is shared by every recipient; each recipient wraps that same CEK
/// using its own key-management algorithm, and any algorithm-specific key-management parameters (e.g. <c>epk</c>,
/// <c>iv</c>, <c>tag</c>, <c>p2s</c>, <c>p2c</c>) are emitted into this recipient's header.
/// </summary>
/// <param name="keyManagementCredentials">The <see cref="JoseKeyManagementCredentials"/> that are used to wrap the
/// shared content-encryption key (CEK) for this recipient.</param>
[PublicAPI]
public sealed class JoseRecipientOptions(JoseKeyManagementCredentials keyManagementCredentials)
{
    /// <summary>
    /// Gets the <see cref="JoseKeyManagementCredentials"/> that are used to wrap the shared content-encryption
    /// key (CEK) for this recipient.
    /// </summary>
    public JoseKeyManagementCredentials KeyManagementCredentials { get; } =
        keyManagementCredentials;

    /// <summary>
    /// Gets a boolean indicating whether to add the secret's <c>kid</c> value to this recipient's header.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool AddKeyIdHeader { get; init; } = true;

    /// <summary>
    /// Gets any additional members to include in this recipient's header.
    /// The default value is <c>null</c>.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? Headers { get; init; }
}
