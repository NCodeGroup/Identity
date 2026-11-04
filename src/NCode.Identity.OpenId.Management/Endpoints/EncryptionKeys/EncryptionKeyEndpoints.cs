// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.EncryptionKeys;

/// <summary>
/// Provides the status and rekey operations that the tenant and server endpoint handlers share, once they have found
/// the owner and authorized the caller: the same master key lifecycle serves both.
/// </summary>
// See ADR-1407.
internal static class EncryptionKeyEndpoints
{
    /// <summary>
    /// The detail of the problem that a rekey which lost a race to another rekey answers with.
    /// </summary>
    internal const string RekeyConflictDetail =
        "Another rotation of this master key committed first; read the encryption keys and retry.";

    /// <summary>
    /// Answers with the master keys of the owner.
    /// </summary>
    public static async ValueTask<IResult> GetAsync(
        IMasterKeyManager masterKeyManager,
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var status = await masterKeyManager.GetStatusAsync(owner, cancellationToken);

        return TypedResults.Ok(
            new EncryptionKeysResource
            {
                Keys = [.. status.Keys.Select(ToResource)],
                RecoveryConfigured = status.RecoveryConfigured,
            }
        );
    }

    /// <summary>
    /// Rotates the master key of the owner and answers with the new active key and the retired one. A rekey that lost
    /// a race to another rekey answers <c>409 Conflict</c>.
    /// </summary>
    public static async ValueTask<IResult> RekeyAsync(
        IMasterKeyManager masterKeyManager,
        IManagementAuditRecorder auditRecorder,
        HttpContext httpContext,
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        MasterKeyRotation rotation;
        try
        {
            rotation = await masterKeyManager.RotateAsync(owner, cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            return TypedResults.Problem(
                detail: RekeyConflictDetail,
                statusCode: StatusCodes.Status409Conflict
            );
        }

        await auditRecorder.RecordMasterKeyRotatedAsync(httpContext, rotation, cancellationToken);

        return TypedResults.Ok(
            new EncryptionKeyRotationResource
            {
                ActiveKey = ToResource(rotation.ActiveKey),
                RetiredKey = rotation.RetiredKey is null ? null : ToResource(rotation.RetiredKey),
            }
        );
    }

    private static EncryptionKeyResource ToResource(MasterKeyInfo key) =>
        new()
        {
            KeyId = key.KeyId,
            Version = key.Version,
            State = key.IsActive ? EncryptionKeyStates.Active : EncryptionKeyStates.Retired,
            CreatedWhen = key.CreatedWhen,
            RetiredWhen = key.RetiredWhen,
            RootKeyProvider = key.RootKeyProvider,
            RootKeyId = key.RootKeyId,
            RecoveryKeyId = key.RecoveryKeyId,
        };
}
