// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Contains the public half of a key that a key management service holds, with the reference to the key, to bind it to
/// a tenant as a <c>remote</c> secret.
/// </summary>
// See ADR-1475.
[ExcludeFromCodeCoverage]
internal sealed class RemoteSecretImportRequest
{
    /// <summary>
    /// Gets the public half of the key and the metadata to import it with, under the client-key policy.
    /// </summary>
    public required SecretKeyImportRequest Key { get; init; }

    /// <summary>
    /// Gets the reference to the key in the key management service of a remote key provider.
    /// </summary>
    public required RemoteKeyReference Reference { get; init; }
}
