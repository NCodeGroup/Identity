// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

/// <summary>
/// Builds the secrets and specifications the tenant key rollover tests share.
/// </summary>
internal static class TenantKeyTestData
{
    public static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    public static TenantKeySpecification Es256 { get; } =
        new()
        {
            Algorithm = "ES256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
        };

    /// <summary>
    /// Creates a generated RSA key with the specified lifecycle, bound to the specified algorithm and, unless told
    /// otherwise, managed by the rollover.
    /// </summary>
    public static PersistedSecret CreateSecret(
        string secretId,
        DateTimeOffset createdWhen,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset expiresWhen,
        string? algorithm = "RS256",
        string? managedBy = SecretManagers.TenantKeyRollover
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            Use = SecretKeyUses.Signature,
            Algorithm = algorithm,
            CreatedWhen = createdWhen,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = expiresWhen,
            SecretType = SecretKeyTypes.Rsa,
            SecretEncoding = SecretEncodings.Pkcs8,
            ManagedBy = managedBy,
            KeySizeBits = 3072,
            PublicKey = "AQ",
        }.Build();
}
