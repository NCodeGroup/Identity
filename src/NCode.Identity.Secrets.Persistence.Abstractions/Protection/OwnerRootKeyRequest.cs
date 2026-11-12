// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Describes the root key that an owner brings: the key wrapper, the key it names and the identity that reaches it.
/// </summary>
/// <param name="KeyWrapperName">The name of the key wrapper, which has to be allowed.</param>
/// <param name="KeyReference">The reference to the key.</param>
// See ADR-1414.
[PublicAPI]
public sealed record OwnerRootKeyRequest(string KeyWrapperName, string KeyReference)
{
    /// <summary>
    /// Gets the identity that the owner grants and the server takes on to reach the key, such as an AWS IAM role ARN, a
    /// Google Cloud service account email or a Microsoft Entra directory ID; or <see langword="null"/> when the server
    /// reaches the key as itself.
    /// </summary>
    public string? AccessIdentity { get; init; }
}
