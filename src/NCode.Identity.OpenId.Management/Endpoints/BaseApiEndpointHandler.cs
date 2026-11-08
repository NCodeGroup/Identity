// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Extends <see cref="ManagementApiEndpointHandler"/> with the management implementation's own secret- and
/// settings-projection helpers shared by the client, tenant, team, and platform endpoint handlers.
/// </summary>
internal abstract class BaseApiEndpointHandler : ManagementApiEndpointHandler
{
    /// <summary>
    /// Serializes the specified <see cref="JsonObject"/> into a detached <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to serialize.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    internal virtual JsonElement SerializeToElement(JsonObject jsonObject)
    {
        return JsonSerializer.SerializeToElement(jsonObject);
    }

    /// <summary>
    /// Converts the specified <see cref="JsonElement"/> into a mutable <see cref="JsonObject"/>, returning an empty
    /// object when the element is <c>null</c> or not a JSON object.
    /// </summary>
    /// <param name="element">The <see cref="JsonElement"/> to convert.</param>
    /// <returns>The resulting <see cref="JsonObject"/>.</returns>
    internal virtual JsonObject ToJsonObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Object:
                return JsonObject.Create(element) ?? [];

            default:
                return [];
        }
    }

    /// <summary>
    /// Serializes an effective (merged) <see cref="IReadOnlySettingCollection"/> into a flat <see cref="JsonElement"/>
    /// of setting name to formatted value, ordered by name for a stable dump and using the same value representation
    /// as the discovery document. Only the settings that apply within the scope are included, so the preview shows what
    /// governs that scope and omits what the runtime never reads there — a tenant-wide session setting in a client's
    /// preview, or a client setting in a resource server's.
    /// </summary>
    /// <param name="settings">The effective <see cref="IReadOnlySettingCollection"/> to serialize.</param>
    /// <param name="scopes">The registered <see cref="ISettingScopeCollection"/> that decides where a setting applies.</param>
    /// <param name="scopeName">The name of the scope whose effective settings <paramref name="settings"/> are.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    // See ADR-1149.
    internal virtual JsonElement ToEffectiveSettingsElement(
        IReadOnlySettingCollection settings,
        ISettingScopeCollection scopes,
        string scopeName
    )
    {
        var values = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var setting in settings)
        {
            var descriptor = setting.Descriptor;
            if (scopes.AppliesWithin(descriptor, scopeName))
            {
                values[descriptor.Name] = descriptor.Format(setting);
            }
        }

        return JsonSerializer.SerializeToElement(values);
    }

    /// <summary>
    /// The most errors a validation problem lists for one setting.
    /// </summary>
    internal const int MaxErrorsPerSetting = 10;

    /// <summary>
    /// Limits the number of errors listed for each setting: the first errors of a setting are kept in order, and the
    /// errors of a setting beyond the limit are replaced by one error that counts them. The counting errors follow the
    /// kept errors, in the order the settings first appear.
    /// </summary>
    /// <param name="errors">The <see cref="SettingError"/> instances to limit.</param>
    /// <param name="maxErrorsPerSetting">The most errors to keep for one setting.</param>
    /// <returns>The kept errors followed by one counting error for each setting that exceeds the limit.</returns>
    internal static IReadOnlyList<SettingError> LimitErrorsPerSetting(
        IReadOnlyList<SettingError> errors,
        int maxErrorsPerSetting
    )
    {
        var kept = new List<SettingError>(errors.Count);
        var names = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var error in errors)
        {
            if (!counts.TryGetValue(error.SettingName, out var count))
            {
                names.Add(error.SettingName);
            }

            counts[error.SettingName] = count + 1;
            if (count < maxErrorsPerSetting)
            {
                kept.Add(error);
            }
        }

        foreach (var name in names)
        {
            var count = counts[name];
            if (count > maxErrorsPerSetting)
            {
                kept.Add(
                    new SettingError(
                        name,
                        $"Errors not listed for '{name}': {count - maxErrorsPerSetting}."
                    )
                );
            }
        }

        return kept;
    }

    /// <summary>
    /// Builds the <c>400 Bad Request</c> validation problem for a settings document that cannot be honored: the
    /// problem's <c>errors</c> map each offending setting name to its errors, and its <c>detail</c> lists them. A
    /// tenant, team or platform change can report one error per client, so each setting lists at most
    /// <see cref="MaxErrorsPerSetting"/> errors and then counts the rest.
    /// </summary>
    /// <param name="errors">The <see cref="SettingError"/> instances describing each setting that cannot be honored.</param>
    /// <returns>The <see cref="IResult"/> representing the validation failure.</returns>
    internal virtual IResult SettingsValidationProblem(IReadOnlyList<SettingError> errors)
    {
        errors = LimitErrorsPerSetting(errors, MaxErrorsPerSetting);
        var messages = errors.Select(error => error.Message);
        var detail = string.Join(" ", messages);
        var errorsBySetting = errors
            .GroupBy(error => error.SettingName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal
            );

        return TypedResults.ValidationProblem(
            errorsBySetting,
            detail: detail,
            title: "The settings are invalid."
        );
    }

    /// <summary>
    /// Gets the refusal of a settings write that changes limits without <see cref="Operations.Update"/> authority at the
    /// node above the one whose document carries them, or <c>null</c> when the write may proceed.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="changedLimits">The names of the limits the write adds, changes or removes.</param>
    /// <param name="authorityAbove">The node above the one whose settings document carries the limits.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <c>403 Forbidden</c> result, or <c>null</c> when the write changes no limit or the caller may update <paramref name="authorityAbove"/>.</returns>
    // See ADR-1418.
    internal virtual async ValueTask<IResult?> GetLimitChangeRefusalOrDefaultAsync(
        HttpContext httpContext,
        IReadOnlyList<string> changedLimits,
        ISupportTenantId authorityAbove,
        CancellationToken cancellationToken
    )
    {
        if (changedLimits.Count == 0)
        {
            return null;
        }

        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            authorityAbove,
            Operations.Update
        );
        if (authorizationResult.Succeeded)
        {
            return null;
        }

        var names = string.Join(", ", changedLimits.Select(name => $"'{name}'"));
        return TypedResults.Problem(
            detail: $"The caller may not change the limits {names}: a limit is set by the administrators above the resource whose settings carry it.",
            statusCode: StatusCodes.Status403Forbidden
        );
    }

    /// <summary>
    /// Processes an HTTP <c>GET</c> request for the limits a settings document may carry: authorizes
    /// <see cref="Operations.Read"/> at the node, reads the node above it (<c>404 Not Found</c> when it is absent), and
    /// answers the limits together with whether the caller may update the node above. The answer is specific to the
    /// caller and carries no <c>ETag</c>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="node">The node whose settings document carries the limits.</param>
    /// <param name="limits">The names of the limits the document may carry.</param>
    /// <param name="readNodeAboveAsync">A function that reads the node above <paramref name="node"/>, or returns <c>null</c> when not found.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    // See ADR-1448.
    internal virtual ValueTask<IResult> ProcessGetSettingLimitsAsync(
        HttpContext httpContext,
        ResourceNode node,
        IReadOnlyList<string> limits,
        Func<CancellationToken, ValueTask<ResourceNode?>> readNodeAboveAsync,
        CancellationToken cancellationToken
    )
    {
        async ValueTask<SettingLimitsResource> MapAsync(
            ResourceNode nodeAbove,
            CancellationToken token
        )
        {
            var authorizationResult = await AuthorizationService.AuthorizeAsync(
                httpContext.User,
                nodeAbove,
                Operations.Update
            );

            return new SettingLimitsResource
            {
                Names = [.. limits],
                CanChange = authorizationResult.Succeeded,
            };
        }

        return ProcessGetAsync(
            httpContext,
            node,
            Operations.Read,
            readNodeAboveAsync,
            MapAsync,
            cancellationToken
        );
    }

    /// <summary>
    /// Copies a persisted secret with the use, algorithm and lifecycle of an update, keeping its identity, concurrency
    /// token, origin, manager and key material. An instant the update omits keeps the stored one, the retirement
    /// bounded by the new expiry, so a retired or upcoming key stays so unless the update names the instant.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> to copy.</param>
    /// <param name="request">The <see cref="UpdateSecretRequest"/> whose metadata the copy takes.</param>
    /// <returns>The copy of <paramref name="secret"/> with the metadata of <paramref name="request"/>.</returns>
    // See ADR-1403.
    internal static PersistedSecret WithUpdate(PersistedSecret secret, UpdateSecretRequest request)
    {
        var (activatesWhen, retiresWhen) = SecretLifecycle.ResolveUpdate(
            secret,
            request.ActivatesWhen,
            request.RetiresWhen,
            request.ExpiresWhen
        );
        return secret with
        {
            Use = request.Use,
            Algorithm = request.Algorithm,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = request.ExpiresWhen,
        };
    }

    /// <summary>
    /// Maps a collection of <see cref="PersistedSecret"/> instances to their <see cref="SecretResource"/> representations.
    /// </summary>
    /// <param name="secrets">The collection of <see cref="PersistedSecret"/> instances to map.</param>
    /// <returns>The mapped read-only collection of <see cref="SecretResource"/> instances.</returns>
    internal virtual IReadOnlyCollection<SecretResource> ToSecretsResource(
        IReadOnlyCollection<PersistedSecret> secrets
    )
    {
        return secrets.Select(ToSecretResource).ToList();
    }

    /// <summary>
    /// Maps a single <see cref="PersistedSecret"/> instance to its <see cref="SecretResource"/> representation.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> instance to map.</param>
    /// <returns>The mapped <see cref="SecretResource"/>.</returns>
    internal virtual SecretResource ToSecretResource(PersistedSecret secret)
    {
        return new SecretResource
        {
            SecretId = secret.SecretId,
            KeyId = GetKeyId(secret),
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ActivatesWhen = secret.ActivatesWhen,
            RetiresWhen = secret.RetiresWhen,
            ExpiresWhen = secret.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
            ManagedBy = secret.ManagedBy,
            IsVerifier = SecretEncodings.IsVerifier(secret.SecretEncoding),
            RemoteKeyProvider = GetRemoteKeyProvider(secret),
        };
    }

    /// <summary>
    /// Maps a single <see cref="PersistedSecret"/> instance to its <see cref="CreatedSecretResource"/>
    /// representation, carrying the one-time unprotected key material to reveal. Used only by the
    /// client-secret create path; every read path returns the material-free <see cref="SecretResource"/>.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> instance to map.</param>
    /// <param name="secretMaterial">The one-time unprotected material to reveal, or <c>null</c> when none.</param>
    /// <returns>The mapped <see cref="CreatedSecretResource"/>.</returns>
    internal virtual CreatedSecretResource ToCreatedSecretResource(
        PersistedSecret secret,
        string? secretMaterial
    )
    {
        return new CreatedSecretResource
        {
            SecretId = secret.SecretId,
            KeyId = GetKeyId(secret),
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ActivatesWhen = secret.ActivatesWhen,
            RetiresWhen = secret.RetiresWhen,
            ExpiresWhen = secret.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
            ManagedBy = secret.ManagedBy,
            IsVerifier = SecretEncodings.IsVerifier(secret.SecretEncoding),
            RemoteKeyProvider = GetRemoteKeyProvider(secret),
            SecretMaterial = secretMaterial,
        };
    }

    /// <summary>
    /// Gets the key identifier (<c>kid</c>) a key is selected by: the <c>kid</c> it was imported with, or its secret
    /// identifier when it has none.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> to identify.</param>
    /// <returns>The key identifier.</returns>
    internal static string GetKeyId(PersistedSecret secret) => secret.KeyId ?? secret.SecretId;

    /// <summary>
    /// Gets the name of the remote key provider whose key management service holds a secret's private half. The key
    /// management service's own key identifier is never returned: it names the deployment's infrastructure.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> to inspect.</param>
    /// <returns>
    /// The provider name, or <c>null</c> when the secret is not a remote key or its reference is malformed.
    /// </returns>
    internal static string? GetRemoteKeyProvider(PersistedSecret secret) =>
        SecretEncodings.IsRemote(secret.SecretEncoding)
        && RemoteKeyReference.TryParse(secret.EncodedValue, out var reference)
            ? reference.Provider
            : null;

    /// <summary>
    /// Returns a <c>409 Conflict</c> when one of an owner's existing keys is selected by the same <c>kid</c> as an
    /// imported key, because keys are selected by <c>kid</c> and two of one owner's keys never share one.
    /// </summary>
    /// <param name="secrets">The owner's existing keys.</param>
    /// <param name="imported">The imported key.</param>
    /// <param name="owner">The kind of owner, such as <c>client</c> or <c>tenant</c>, for the problem detail.</param>
    /// <returns>The conflict result, or <c>null</c> when the <c>kid</c> is free.</returns>
    // See ADR-1161 and ADR-1186.
    internal static IResult? KeyIdConflictOrDefault(
        IEnumerable<PersistedSecret> secrets,
        PersistedSecret imported,
        string owner
    )
    {
        var keyId = GetKeyId(imported);
        if (
            !secrets.Any(secret => string.Equals(GetKeyId(secret), keyId, StringComparison.Ordinal))
        )
            return null;

        return TypedResults.Problem(
            detail: $"A key with the 'kid' value '{keyId}' is already registered for the {owner}.",
            statusCode: StatusCodes.Status409Conflict
        );
    }

    /// <summary>
    /// Returns a <c>409 Conflict</c> when one of an owner's secrets already has the identifier, which identifiers
    /// compare without regard to case.
    /// </summary>
    /// <param name="secrets">The owner's existing secrets.</param>
    /// <param name="secretId">The identifier to check.</param>
    /// <returns>The conflict result, or <c>null</c> when the identifier is free.</returns>
    internal static IResult? SecretIdConflictOrDefault(
        IEnumerable<PersistedSecret> secrets,
        string secretId
    )
    {
        if (
            !secrets.Any(secret =>
                string.Equals(secret.SecretId, secretId, StringComparison.OrdinalIgnoreCase)
            )
        )
            return null;

        return TypedResults.Problem(
            detail: "A secret with the specified identifier already exists.",
            statusCode: StatusCodes.Status409Conflict
        );
    }

    /// <summary>
    /// Gets the platform's authority node: the one deployment root, addressed from the control-plane tenant the request
    /// runs under.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The platform's <see cref="ResourceNode"/>.</returns>
    internal static ResourceNode GetPlatformNode(HttpContext httpContext) =>
        ResourceNode.For(
            httpContext.GetOpenIdContext().Tenant.TenantId,
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId
        );

    /// <summary>
    /// Gets a team's authority node, addressed from the control-plane tenant the request runs under.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="teamId">The identifier of the team.</param>
    /// <returns>The team's <see cref="ResourceNode"/>.</returns>
    internal static ResourceNode GetTeamNode(HttpContext httpContext, string teamId) =>
        ResourceNode.For(
            httpContext.GetOpenIdContext().Tenant.TenantId,
            ResourceNodeTypes.Team,
            teamId
        );

    /// <summary>
    /// Gets a tenant's authority node: its own id addresses the tenant node.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant.</param>
    /// <returns>The tenant's <see cref="ResourceNode"/>.</returns>
    internal static ResourceNode GetTenantNode(string tenantId) =>
        ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);

    /// <summary>
    /// Announces a committed change to a tenant: this replica evicts it from the tenant cache, so the change takes
    /// effect at once, and seeds it after a settings change, so a key its new settings require is provisioned. The
    /// mediator is request-scoped, so it is resolved from the request's services.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="tenantId">The identifier of the tenant that changed.</param>
    /// <param name="settingsChanged">Whether the tenant's settings changed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    // See ADR-1169.
    internal static ValueTask NotifyTenantChangedAsync(
        HttpContext httpContext,
        string tenantId,
        bool settingsChanged,
        CancellationToken cancellationToken
    )
    {
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
        return mediator.SendAsync(
            new TenantChangedCommand(tenantId, settingsChanged),
            cancellationToken
        );
    }
}
