// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.EncryptionKeys;

/// <summary>
/// Provides the status and rekey operations that the tenant and platform endpoint handlers share, once they have found
/// the owner and authorized the caller: the same master key lifecycle serves both.
/// </summary>
// See ADR-1407 and ADR-1414.
internal static class EncryptionKeyEndpoints
{
    /// <summary>
    /// The detail of the problem that a rekey within twice the refresh interval of the last rotation answers with.
    /// </summary>
    internal const string RekeyTooSoonDetail =
        "The last rotation of this master key has not reached every replica yet; retry after the time in Retry-After.";

    /// <summary>
    /// The title of the problem that a refused owner answers with when it sets a root key.
    /// </summary>
    internal const string RootKeyNotAllowedTitle = "The tenant cannot bring a root key.";

    /// <summary>
    /// Answers <c>400 Bad Request</c> for an owner that a registered <see cref="IOwnerRootKeyPolicy"/> refuses.
    /// </summary>
    /// <param name="reason">The reason that the policy gave.</param>
    public static ProblemHttpResult RootKeyNotAllowed(string reason) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: RootKeyNotAllowedTitle,
            detail: reason
        );

    /// <summary>
    /// Answers with the master keys of the owner, the root key it brings and the key wrappers it may choose. The caller
    /// passes no <paramref name="ownerRootKeyManager"/> for an owner that cannot bring a root key, such as the server.
    /// </summary>
    public static async ValueTask<IResult> GetAsync(
        IMasterKeyManager masterKeyManager,
        IOwnerRootKeyManager? ownerRootKeyManager,
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
                RootKey = status.RootKey is null ? null : ToResource(status.RootKey),
                RootKeyProviders = [.. ownerRootKeyManager?.AllowedKeyWrapperNames ?? []],
                RootKeyAccessBinding = ownerRootKeyManager?.GetAccessBinding(owner),
            }
        );
    }

    /// <summary>
    /// Rotates the master key of the owner and answers with the new active key and the retired one. A rekey within
    /// twice the refresh interval of the owner's last rotation answers <c>409 Conflict</c> with a <c>Retry-After</c>
    /// header; one that loses a race to another rekey throws <see cref="StoreConcurrencyException"/>, which the
    /// management route group answers <c>412</c>.
    /// </summary>
    // See ADR-1214.
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
        catch (MasterKeyRotationTooSoonException exception)
        {
            return RekeyTooSoon(httpContext, exception);
        }

        await auditRecorder.RecordMasterKeyRotatedAsync(
            httpContext,
            rotation,
            MasterKeyChangeReasons.Requested,
            cancellationToken
        );

        return TypedResults.Ok(
            new EncryptionKeyRotationResource
            {
                ActiveKey = ToResource(rotation.ActiveKey),
                RetiredKey = rotation.RetiredKey is null ? null : ToResource(rotation.RetiredKey),
            }
        );
    }

    /// <summary>
    /// Sets or changes the root key of the owner. The request is checked field by field and answered <c>400</c> with a
    /// problem that names the field; an owner that a registered <see cref="IOwnerRootKeyPolicy"/> refuses is answered
    /// <c>400</c> with the policy's reason; a key that fails its probe is answered <c>422</c>; a change within twice the
    /// refresh interval of the owner's last rotation is answered <c>409 Conflict</c> with a <c>Retry-After</c> header;
    /// one that loses a race throws <see cref="StoreConcurrencyException"/>, which the management route group answers
    /// <c>412</c>.
    /// </summary>
    public static async ValueTask<IResult> SetRootKeyAsync(
        IOwnerRootKeyManager ownerRootKeyManager,
        IManagementAuditRecorder auditRecorder,
        HttpContext httpContext,
        SecretOwner owner,
        EncryptionRootKeyRequest request,
        CancellationToken cancellationToken
    )
    {
        var errors = ValidateRequest(ownerRootKeyManager, request);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        OwnerRootKeyChange change;
        try
        {
            change = await ownerRootKeyManager.SetAsync(
                owner,
                new OwnerRootKeyRequest(
                    request.Provider,
                    request.KeyReference,
                    request.AccessIdentity
                ),
                cancellationToken
            );
        }
        catch (MasterKeyRotationTooSoonException exception)
        {
            return RekeyTooSoon(httpContext, exception);
        }
        catch (OwnerRootKeyNotAllowedException exception)
        {
            return RootKeyNotAllowed(exception.Reason);
        }
        catch (OwnerRootKeyProbeException exception)
        {
            // The key the tenant already used failed, so its state changed and the change is audited.
            if (exception.UnavailableRootKey is { } unavailable)
            {
                await auditRecorder.RecordRootKeyChangedAsync(
                    httpContext,
                    unavailable,
                    RootKeyChanges.Unavailable,
                    RootKeyChangeReasons.ValidationFailed,
                    cancellationToken
                );
            }

            return TypedResults.Problem(
                detail: $"The key could not wrap and unwrap a probe through '{request.Provider}': {exception.Failure}",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?>
                {
                    ["provider"] = request.Provider,
                    ["keyReference"] = request.KeyReference,
                    ["accessIdentity"] = request.AccessIdentity,
                }
            );
        }

        if (change.Change is { } changed)
        {
            await auditRecorder.RecordRootKeyChangedAsync(
                httpContext,
                change.RootKey,
                changed,
                RootKeyChangeReasons.Requested,
                cancellationToken
            );
        }

        if (change.Rotation is { } rotation)
        {
            await auditRecorder.RecordMasterKeyRotatedAsync(
                httpContext,
                rotation,
                MasterKeyChangeReasons.RootKeyChanged,
                cancellationToken
            );
        }

        return TypedResults.Ok(
            new EncryptionRootKeyChangeResource
            {
                RootKey = ToResource(change.RootKey),
                ActiveKey = change.Rotation is null ? null : ToResource(change.Rotation.ActiveKey),
                RetiredKey = change.Rotation?.RetiredKey is null
                    ? null
                    : ToResource(change.Rotation.RetiredKey),
                RewrappedKeys = change.Rewrap.Rewrapped,
                FailedKeys = change.Rewrap.Failed,
            }
        );
    }

    /// <summary>
    /// Removes the root key of the owner: <c>204</c> once every master key is under the deployment's root key, also when
    /// the owner had none, and <c>202</c> with the key in the removing state while master keys remain under it.
    /// </summary>
    public static async ValueTask<IResult> RemoveRootKeyAsync(
        IOwnerRootKeyManager ownerRootKeyManager,
        IManagementAuditRecorder auditRecorder,
        HttpContext httpContext,
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        // The removal can delete the record at once, so the event names the key as it stood before.
        var before = await ownerRootKeyManager.GetAsync(owner, cancellationToken);
        var removal = await ownerRootKeyManager.RemoveAsync(owner, cancellationToken);

        if (removal.Removed && before is not null)
        {
            await auditRecorder.RecordRootKeyChangedAsync(
                httpContext,
                before with
                {
                    State = OwnerRootKeyStates.Removing,
                },
                RootKeyChanges.Removed,
                RootKeyChangeReasons.Requested,
                cancellationToken
            );
        }

        return removal.Remaining is null
            ? TypedResults.NoContent()
            : TypedResults.Accepted((string?)null, ToResource(removal.Remaining));
    }

    /// <summary>
    /// Probes the root key of the owner and answers with its state; <c>404</c> when the owner has none.
    /// </summary>
    public static async ValueTask<IResult> ValidateRootKeyAsync(
        IOwnerRootKeyManager ownerRootKeyManager,
        IManagementAuditRecorder auditRecorder,
        HttpContext httpContext,
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var validation = await ownerRootKeyManager.ValidateAsync(owner, cancellationToken);
        if (validation.RootKey is not { } rootKey)
            return TypedResults.NotFound();

        if (validation.Change is { } change)
        {
            await auditRecorder.RecordRootKeyChangedAsync(
                httpContext,
                rootKey,
                change,
                RootKeyChangeReasons.ForValidation(change),
                cancellationToken
            );
        }

        if (validation.Rotation is { } rotation)
        {
            await auditRecorder.RecordMasterKeyRotatedAsync(
                httpContext,
                rotation,
                MasterKeyChangeReasons.RootKeyChanged,
                cancellationToken
            );
        }

        return TypedResults.Ok(ToResource(rootKey));
    }

    private static Dictionary<string, string[]> ValidateRequest(
        IOwnerRootKeyManager ownerRootKeyManager,
        EncryptionRootKeyRequest request
    )
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var provider = request.Provider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            errors["provider"] = ["The key wrapper is required."];
        }
        else if (
            !ownerRootKeyManager.AllowedKeyWrapperNames.Contains(provider, StringComparer.Ordinal)
        )
        {
            var allowed = ownerRootKeyManager.AllowedKeyWrapperNames;
            errors["provider"] =
            [
                allowed.Count == 0
                    ? $"The key wrapper '{provider}' is not allowed: this deployment lets no tenant bring a root key."
                    : $"The key wrapper '{provider}' is not allowed. The allowed ones are: {string.Join(", ", allowed)}.",
            ];
        }

        if (string.IsNullOrWhiteSpace(request.KeyReference))
        {
            errors["keyReference"] = ["The key reference is required."];
        }
        else if (
            !errors.ContainsKey("provider")
            && !ownerRootKeyManager.TryValidateKeyReference(
                provider,
                request.KeyReference,
                out var failure
            )
        )
        {
            errors["keyReference"] = [failure];
        }

        if (request.AccessIdentity is { } accessIdentity)
        {
            if (string.IsNullOrWhiteSpace(accessIdentity))
            {
                errors["accessIdentity"] =
                [
                    "The access identity must not be blank; omit it to reach the key with the deployment's identity.",
                ];
            }
            else if (
                !errors.ContainsKey("provider")
                && !ownerRootKeyManager.TryValidateAccessIdentity(
                    provider,
                    accessIdentity,
                    out var accessFailure
                )
            )
            {
                errors["accessIdentity"] = [accessFailure];
            }
        }

        return errors;
    }

    private static ProblemHttpResult RekeyTooSoon(
        HttpContext httpContext,
        MasterKeyRotationTooSoonException exception
    )
    {
        httpContext.Response.Headers.RetryAfter = Math.Max(
                1,
                (long)Math.Ceiling(exception.RetryAfter.TotalSeconds)
            )
            .ToString(CultureInfo.InvariantCulture);

        return TypedResults.Problem(
            detail: RekeyTooSoonDetail,
            statusCode: StatusCodes.Status409Conflict
        );
    }

    private static EncryptionRootKeyResource ToResource(OwnerRootKeyInfo rootKey) =>
        new()
        {
            Provider = rootKey.KeyWrapperName,
            KeyReference = rootKey.KeyReference,
            AccessIdentity = rootKey.AccessIdentity,
            State = rootKey.State,
            CreatedWhen = rootKey.CreatedWhen,
            StateChangedWhen = rootKey.StateChangedWhen,
            ValidatedWhen = rootKey.ValidatedWhen,
            FailedWhen = rootKey.FailedWhen,
            Failure = rootKey.Failure,
        };

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
            RootKeyReference = key.RootKeyReference,
            RootKeyAccessIdentity = key.RootKeyAccessIdentity,
        };
}
