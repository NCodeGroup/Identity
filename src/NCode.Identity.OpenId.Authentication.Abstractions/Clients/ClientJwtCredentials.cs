// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the credentials that protect a JWT the server addresses to a client: the tenant's key that signs it and the
/// client's key it is encrypted to. A response with neither is returned unprotected.
/// </summary>
/// <param name="SigningCredentials">The credentials that sign the JWT, or <c>null</c> when it is not signed.</param>
/// <param name="EncryptionCredentials">The credentials that encrypt the JWT, or <c>null</c> when it is not
/// encrypted.</param>
[PublicAPI]
public readonly record struct ClientJwtCredentials(
    JoseSigningCredentials? SigningCredentials,
    JoseEncryptionCredentials? EncryptionCredentials
)
{
    /// <summary>
    /// Gets a value indicating whether the JWT is signed, encrypted, or both.
    /// </summary>
    public bool IsProtected => SigningCredentials is not null || EncryptionCredentials is not null;
}
